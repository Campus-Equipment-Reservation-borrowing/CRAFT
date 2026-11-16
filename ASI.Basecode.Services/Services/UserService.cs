using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ViewModelClasses;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using ASI.Basecode.Data.QueryModels;
using ErrorMessages = ASI.Basecode.Resources.Messages.Errors;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// User management service: validation, uniqueness rules, staff provisioning (temporary
    /// passwords), last-admin / self-edit guards and audit logging.
    /// Data access stays in the repository; this layer owns the business rules.
    /// </summary>
    public class UserService : ServiceBase, IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IReservationRepository _reservationRepository;
        private readonly ICustodianAssignmentRepository _custodianAssignments;
        private readonly INotificationRepository _notificationRepository;
        private readonly ILookupRepository _lookups;
        private readonly IAuditLogRepository _auditLogs;
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUser;
        private readonly INotificationDispatcher _notifications;
        private readonly ICustodianAssignmentService _assignments;
        private readonly IMapper _mapper;

        /// <summary>Direct EF access for the small writes this service owns.</summary>
        public UserService(
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            IUserRepository userRepository,
            ICurrentUserService currentUser,
            INotificationDispatcher notifications,
            ICustodianAssignmentService assignments,
            IMapper mapper,
            IAuditLogRepository auditLogs,
            ILookupRepository lookups,
            INotificationRepository notificationRepository,
            ICustodianAssignmentRepository custodianAssignments,
            IReservationRepository reservationRepository)
            : base(loggerFactory)
        {
            this._reservationRepository = reservationRepository;
            this._custodianAssignments = custodianAssignments;
            this._notificationRepository = notificationRepository;
            this._lookups = lookups;
            this._auditLogs = auditLogs;
            this._unitOfWork = unitOfWork;
            this._userRepository = userRepository;
            this._currentUser = currentUser;
            this._notifications = notifications;
            this._assignments = assignments;
            this._mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public VerificationQueueViewModel GetVerificationQueue(string tab, string search, int page, int pageSize)
        {
            var rejectedTab = string.Equals(tab, "rejected", StringComparison.OrdinalIgnoreCase);
            var statusCode = rejectedTab ? AccountStatusCodes.Rejected : AccountStatusCodes.Unverified;
            var statusIds = this._lookups.GetAccountStatusIdsByCode();

            var result = statusIds.TryGetValue(statusCode, out var statusId)
                ? this._userRepository.GetPaged(search, null, null, statusId, page, pageSize)
                : new PagedResult<User> { Page = 1, PageSize = pageSize };

            int CountOf(string code) => statusIds.TryGetValue(code, out var id)
                ? this._userRepository.CountWithAccountStatus(id)
                : 0;

            var userIds = result.Items.Select(u => u.UserId).ToList();
            var currentCustodians = this._custodianAssignments.GetCustodianByBorrower(userIds);

            return new VerificationQueueViewModel
            {
                CustodianOptions = this._assignments.GetCustodianOptions(),
                CurrentCustodianByUser = currentCustodians,
                Page = new PagedResult<UserListViewModel>
                {
                    Items = result.Items.Select(entity => this._mapper.Map<UserListViewModel>(entity)).ToList(),
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount,
                },
                Tab = rejectedTab ? "rejected" : "pending",
                Search = search,
                PendingCount = CountOf(AccountStatusCodes.Unverified),
                RejectedCount = CountOf(AccountStatusCodes.Rejected),
            };
        }

        public (bool Success, string Error) Verify(long userId, long custodianUserId)
        {
            try
            {
                if (custodianUserId <= 0)
                {
                    return (false, ErrorMessages.VerificationCustodianRequired);
                }

                var entity = this._userRepository.GetTrackedByIdWithStatusAndRole(userId);
                if (entity == null)
                {
                    return (false, ErrorMessages.UserNotFound);
                }

                if (entity.Role?.IsBorrowerRole != true)
                {
                    return (false, ErrorMessages.VerificationBorrowerOnly);
                }

                var code = entity.AccountStatus?.Code;
                if (code != AccountStatusCodes.Unverified && code != AccountStatusCodes.Rejected)
                {
                    return (false, ErrorMessages.VerificationNotPending);
                }

                var active = this._lookups.GetRequiredTrackedAccountStatusByCode(AccountStatusCodes.Active);
                var now = DateTime.UtcNow;
                var actorId = this._currentUser.GetActorUserId();

                // Activation and custodian assignment succeed or fail together.
                using var transaction = this._unitOfWork.BeginTransaction();
                var staged = this._assignments.StageBorrowerAssignment(entity.UserId, custodianUserId);
                if (!staged.Success)
                {
                    return (false, staged.Error);
                }

                entity.AccountStatusId = active.AccountStatusId;
                entity.VerificationNote = null;
                entity.VerifiedAtUtc = now;
                entity.VerifiedByUserId = actorId;
                this._unitOfWork.SaveChanges();

                this.AddAudit("VERIFY", "User", entity.UserId, entity.AccountIdentifier,
                    $"Verified account '{entity.FirstName} {entity.LastName}' ({entity.AccountIdentifier}) and assigned custodian {staged.CustodianName}.");
                this.AddAudit("ASSIGN", "CustodianAssignment", staged.Assignment.AssignmentId, entity.AccountIdentifier, staged.Details);
                this._unitOfWork.SaveChanges();
                transaction.Commit();

                this._notifications.Notify(entity.UserId, NotificationTypes.Account,
                    "Account verified",
                    $"Your account has been verified. {staged.CustodianName} is your custodian; your reservations will be reviewed by them.",
                    "/Reservations/Available");
                this._notifications.Notify(custodianUserId, NotificationTypes.Account,
                    "New borrower assigned",
                    $"{entity.FirstName} {entity.LastName} ({entity.AccountIdentifier}) was verified and assigned to you. Their reservations will be routed to you.");

                return (true, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                return (false, ErrorMessages.UserUpdateConflict);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to verify user {UserId}.", userId);
                return (false, ErrorMessages.ServerError);
            }
        }

        /// <inheritdoc />
        public List<LookupViewModel> GetCustodianOptions() => this._assignments.GetCustodianOptions();

        public (bool Success, string Error) RejectVerification(long userId, string reason)
        {
            try
            {
                reason = reason?.Trim();
                if (string.IsNullOrWhiteSpace(reason))
                {
                    return (false, ErrorMessages.VerificationReasonRequired);
                }

                if (reason.Length > 500)
                {
                    reason = reason.Substring(0, 500);
                }

                var entity = this._userRepository.GetTrackedByIdWithStatus(userId);
                if (entity == null)
                {
                    return (false, ErrorMessages.UserNotFound);
                }

                if (entity.AccountStatus?.Code != AccountStatusCodes.Unverified)
                {
                    return (false, ErrorMessages.VerificationNotPending);
                }

                var rejected = this._lookups.GetTrackedAccountStatusByCode(AccountStatusCodes.Rejected);
                if (rejected == null)
                {
                    return (false, ErrorMessages.ServerError);
                }

                entity.AccountStatusId = rejected.AccountStatusId;
                entity.VerificationNote = reason;
                entity.VerifiedAtUtc = DateTime.UtcNow;
                entity.VerifiedByUserId = this._currentUser.GetActorUserId();
                this._unitOfWork.SaveChanges();

                this.WriteAudit("REJECT", "User", entity.UserId, entity.AccountIdentifier,
                    $"Rejected verification for '{entity.FirstName} {entity.LastName}' ({entity.AccountIdentifier}). Reason: {reason}");

                this._notifications.Notify(entity.UserId, NotificationTypes.Account,
                    "Account verification rejected",
                    $"Your account could not be verified: {reason} You can submit an appeal from your dashboard.",
                    "/");

                return (true, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                return (false, ErrorMessages.UserUpdateConflict);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to reject verification for user {UserId}.", userId);
                return (false, ErrorMessages.ServerError);
            }
        }

        public UserIndexViewModel GetIndexViewModel(
            string search,
            long? roleId,
            long? departmentId,
            long? accountStatusId,
            int page,
            int pageSize)
        {
            var result = this._userRepository.GetPaged(
                search, roleId, departmentId, accountStatusId, page, pageSize);

            return new UserIndexViewModel
            {
                Page = new PagedResult<UserListViewModel>
                {
                    Items = result.Items.Select(entity => this._mapper.Map<UserListViewModel>(entity)).ToList(),
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount,
                },
                Totals = this._userRepository.GetTotals(),
                Search = search,
                RoleId = roleId,
                DepartmentId = departmentId,
                AccountStatusId = accountStatusId,
                PageSize = result.PageSize,
            };
        }

        public User GetByIdWithDetails(long userId)
        {
            return this._userRepository.GetByIdWithDetails(userId);
        }

        public UserFormViewModel GetForEdit(long userId)
        {
            var entity = this._userRepository.GetByIdWithDetails(userId);
            if (entity == null)
            {
                return null;
            }

            return this._mapper.Map<UserFormViewModel>(entity);
        }

        public (bool Success, string Error, long UserId) Save(UserFormViewModel form)
        {
            try
            {
                if (form == null)
                {
                    return (false, ErrorMessages.ServerError, 0);
                }

                form.FirstName = form.FirstName?.Trim();
                form.LastName = form.LastName?.Trim();
                form.AccountIdentifier = form.AccountIdentifier?.Trim();
                form.SchoolEmail = form.SchoolEmail?.Trim();

                if (this._userRepository.IdentifierExists(form.AccountIdentifier, form.UserId))
                {
                    return (false, ErrorMessages.UserIdentifierDuplicate, form.UserId);
                }

                if (this._userRepository.SchoolEmailExists(form.SchoolEmail, form.UserId))
                {
                    return (false, ErrorMessages.UserEmailDuplicate, form.UserId);
                }

                var lifecycleError = this.ValidateLifecycleChange(form);
                if (lifecycleError != null)
                {
                    return (false, lifecycleError, form.UserId);
                }

                return form.UserId > 0
                    ? this.Update(form)
                    : this.Create(form);
            }
            catch (DbUpdateConcurrencyException)
            {
                this._logger.LogWarning("Concurrency conflict while saving user {UserId}.", form?.UserId);
                return (false, ErrorMessages.UserUpdateConflict, form?.UserId ?? 0);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to save user {UserId}.", form?.UserId);
                return (false, ErrorMessages.ServerError, form?.UserId ?? 0);
            }
        }

        public (bool Success, string Error) Delete(long userId)
        {
            try
            {
                var entity = this._userRepository.GetTrackedById(userId);
                if (entity == null)
                {
                    return (false, ErrorMessages.UserNotFound);
                }

                if (entity.UserId == this._currentUser.GetActorUserId())
                {
                    return (false, ErrorMessages.UserSelfDeleteBlocked);
                }

                if (this.IsActiveAdmin(entity.RoleId, entity.AccountStatusId) && !this.OtherActiveAdminExists(entity.UserId))
                {
                    return (false, ErrorMessages.UserLastAdminBlocked);
                }

                if (this._userRepository.HasTransactionReferences(userId))
                {
                    return (false, ErrorMessages.UserDeleteBlocked);
                }

                // Assignment rows use NO ACTION foreign keys, so clear them explicitly.
                var assignments = this._custodianAssignments.GetTrackedInvolvingUser(userId);
                this._custodianAssignments.RemoveRange(assignments);

                // Keep announcements and tickets; just detach them from the deleted account.
                this._userRepository.ClearAnnouncementCreator(userId);
                this._userRepository.ClearSupportTicketAssignee(userId);

                // A user's own inbox goes with them; audit rows stay, with the actor cleared.
                this._notificationRepository.RemoveAllForRecipient(userId);
                this._auditLogs.ClearActor(userId);

                this._userRepository.Remove(entity);
                this._unitOfWork.SaveChanges();

                this.WriteAudit("DELETE", "User", entity.UserId, entity.AccountIdentifier,
                    $"Deleted user account '{entity.FirstName} {entity.LastName}' ({entity.AccountIdentifier}).");

                return (true, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                this._logger.LogWarning("Concurrency conflict while deleting user {UserId}.", userId);
                return (false, ErrorMessages.UserUpdateConflict);
            }
            catch (DbUpdateException ex)
            {
                // A history row not covered above still references the account.
                this._logger.LogWarning(ex, "User {UserId} is still referenced and cannot be deleted.", userId);
                return (false, ErrorMessages.UserDeleteBlocked);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to delete user {UserId}.", userId);
                return (false, ErrorMessages.ServerError);
            }
        }

        public List<LookupViewModel> GetRoleOptions()
        {
            return this._lookups.GetRoleOptions()
                .Select(o => new LookupViewModel { Id = o.Id, Text = o.Text }).ToList();
        }

        public List<LookupViewModel> GetDepartmentOptions()
        {
            return this._lookups.GetActiveDepartmentOptions()
                .Select(o => new LookupViewModel { Id = o.Id, Text = o.Text }).ToList();
        }

        public List<LookupViewModel> GetAccountStatusOptions()
        {
            return this._lookups.GetAccountStatusOptions()
                .Select(o => new LookupViewModel { Id = o.Id, Text = o.Text }).ToList();
        }

        /// <inheritdoc />
        public List<UserListViewModel> GetOperatorOptions()
        {
            return this._userRepository.GetOperatorOptions()
                .Select(entity => this._mapper.Map<UserListViewModel>(entity))
                .ToList();
        }

        public (bool Success, string Error, string TemporaryPassword) ResetPassword(long userId)
        {
            try
            {
                var entity = this._userRepository.GetTrackedById(userId);
                if (entity == null)
                {
                    return (false, ErrorMessages.UserNotFound, null);
                }

                var temporary = PasswordManager.GenerateTemporaryPassword();
                entity.PasswordHash = PasswordManager.Hash(temporary);
                entity.MustChangePassword = true;
                entity.FailedLoginCount = 0;
                entity.LockoutUntilUtc = null;
                this._unitOfWork.SaveChanges();

                this.WriteAudit("PASSWORD_RESET", "User", entity.UserId, entity.AccountIdentifier,
                    $"Administrator issued a temporary password for '{entity.FirstName} {entity.LastName}' ({entity.AccountIdentifier}).");

                this._notifications.Notify(entity.UserId, NotificationTypes.Account,
                    "Password reset by administrator",
                    "An administrator reset your password. Sign in with the temporary password they gave you and choose a new one.");

                return (true, null, temporary);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to reset password for user {UserId}.", userId);
                return (false, ErrorMessages.ServerError, null);
            }
        }

        private (bool Success, string Error, long UserId) Create(UserFormViewModel form)
        {
            var temporary = string.IsNullOrWhiteSpace(form.TemporaryPassword)
                ? PasswordManager.GenerateTemporaryPassword()
                : form.TemporaryPassword;
            if (!PasswordManager.MeetsPolicy(temporary))
            {
                return (false, ErrorMessages.UserTemporaryPasswordInvalid, 0);
            }

            var entity = this._mapper.Map<User>(form);
            entity.UserId = 0; // ignore any posted id on create
            entity.CreatedAtUtc = default; // let the DB default (sysutcdatetime) fill it
            entity.PasswordHash = PasswordManager.Hash(temporary);
            entity.MustChangePassword = true;

            this._userRepository.Add(entity);
            this._unitOfWork.SaveChanges();

            this.WriteAudit("CREATE", "User", entity.UserId, entity.AccountIdentifier,
                $"Created user account '{entity.FirstName} {entity.LastName}' ({entity.AccountIdentifier}) with a temporary password.");

            form.TemporaryPassword = null;
            form.IssuedTemporaryPassword = temporary;
            return (true, null, entity.UserId);
        }

        private (bool Success, string Error, long UserId) Update(UserFormViewModel form)
        {
            var entity = this._userRepository.GetTrackedById(form.UserId);
            if (entity == null)
            {
                return (false, ErrorMessages.UserNotFound, form.UserId);
            }

            var roleOrStatusChanged = form.RoleId != entity.RoleId || form.AccountStatusId != entity.AccountStatusId;
            if (roleOrStatusChanged && entity.UserId == this._currentUser.GetActorUserId())
            {
                return (false, ErrorMessages.UserSelfChangeBlocked, form.UserId);
            }

            if (roleOrStatusChanged
                && this.IsActiveAdmin(entity.RoleId, entity.AccountStatusId)
                && !this.IsActiveAdmin(form.RoleId ?? 0, form.AccountStatusId ?? 0)
                && !this.OtherActiveAdminExists(entity.UserId))
            {
                return (false, ErrorMessages.UserLastAdminBlocked, form.UserId);
            }

            this._mapper.Map(form, entity);
            entity.UserId = form.UserId;

            this._unitOfWork.SaveChanges();

            this.WriteAudit("UPDATE", "User", entity.UserId, entity.AccountIdentifier,
                $"Updated user account '{entity.FirstName} {entity.LastName}' ({entity.AccountIdentifier}).");

            return (true, null, entity.UserId);
        }

        /// <summary>
        /// Guards the admin user form against the two ways around the custodian rule: activating a
        /// borrower who has no custodian, and taking a custodian out of service while work is still
        /// routed to them.
        /// </summary>
        private string ValidateLifecycleChange(UserFormViewModel form)
        {
            var newRoleId = form.RoleId ?? 0;
            var newStatusId = form.AccountStatusId ?? 0;
            var newRole = this._lookups.GetRoleById(newRoleId);
            var newStatusCode = this._lookups.GetAccountStatusCodeById(newStatusId);
            var current = form.UserId > 0
                ? this._userRepository.GetRoleAndStatusCodes(form.UserId)
                : null;

            var becomesActiveBorrower = newRole?.IsBorrowerRole == true
                && newStatusCode == AccountStatusCodes.Active
                && (current == null || current.StatusCode != AccountStatusCodes.Active || !this.IsBorrowerCode(current.RoleCode));
            if (becomesActiveBorrower
                && (form.UserId <= 0 || !this._custodianAssignments.BorrowerHasAssignment(form.UserId)))
            {
                return ErrorMessages.UserActivateNeedsCustodian;
            }

            var leavesCustodianService = current != null
                && current.RoleCode == RoleCodes.Technician
                && current.StatusCode == AccountStatusCodes.Active
                && (newRole?.RoleCode != RoleCodes.Technician || newStatusCode != AccountStatusCodes.Active);
            if (leavesCustodianService
                && (this._custodianAssignments.CustodianHasAssignment(form.UserId)
                    || this._reservationRepository.AnyAssignedToCustodianInStatuses(form.UserId, OpenReservationStatuses)))
            {
                return ErrorMessages.CustodianDeactivateBlocked;
            }

            return null;
        }

        private static readonly string[] OpenReservationStatuses = { ReservationStatusCodes.Pending, ReservationStatusCodes.Approved, ReservationStatusCodes.ReadyForRelease, ReservationStatusCodes.PartiallyReleased };

        private bool IsBorrowerCode(string roleCode) => roleCode == RoleCodes.Student || roleCode == RoleCodes.Faculty;

        private bool IsActiveAdmin(long roleId, long accountStatusId)
        {
            return this._lookups.RoleHasCode(roleId, RoleCodes.Admin)
                && this._lookups.AccountStatusHasCode(accountStatusId, AccountStatusCodes.Active);
        }

        private bool OtherActiveAdminExists(long excludeUserId)
        {
            return this._userRepository.OtherUserWithRoleAndStatusExists(excludeUserId, RoleCodes.Admin, AccountStatusCodes.Active);
        }

        private void WriteAudit(string actionType, string entityType, long entityId, string referenceNumber, string details)
        {
            this.AddAudit(actionType, entityType, entityId, referenceNumber, details);
            this._unitOfWork.SaveChanges();
        }

        private void AddAudit(string actionType, string entityType, long entityId, string referenceNumber, string details)
        {
            this._auditLogs.Add(new AuditLog
            {
                ActorUserId = this._currentUser.GetActorUserId(),
                ActionType = actionType,
                EntityType = entityType,
                EntityId = entityId,
                ReferenceNumber = referenceNumber,
                Details = details != null && details.Length > 1000 ? details.Substring(0, 1000) : details,
                OccurredAtUtc = DateTime.UtcNow,
            });
        }
    }
}
