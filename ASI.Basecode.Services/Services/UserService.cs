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
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUser;
        private readonly INotificationDispatcher _notifications;
        private readonly IMapper _mapper;

        /// <summary>Direct EF access for the small writes this service owns.</summary>
        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public UserService(
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            IUserRepository userRepository,
            ICurrentUserService currentUser,
            INotificationDispatcher notifications,
            IMapper mapper)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
            this._userRepository = userRepository;
            this._currentUser = currentUser;
            this._notifications = notifications;
            this._mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public VerificationQueueViewModel GetVerificationQueue(string tab, string search, int page, int pageSize)
        {
            var rejectedTab = string.Equals(tab, "rejected", StringComparison.OrdinalIgnoreCase);
            var statusCode = rejectedTab ? AccountStatusCodes.Rejected : AccountStatusCodes.Unverified;
            var statusIds = this.DbContext.AccountStatuses
                .AsNoTracking()
                .ToDictionary(s => s.Code, s => s.AccountStatusId);

            var result = statusIds.TryGetValue(statusCode, out var statusId)
                ? this._userRepository.GetPaged(search, null, null, statusId, page, pageSize)
                : new PagedResult<User> { Page = 1, PageSize = pageSize };

            int CountOf(string code) => statusIds.TryGetValue(code, out var id)
                ? this.DbContext.Users.Count(u => u.AccountStatusId == id)
                : 0;

            return new VerificationQueueViewModel
            {
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

        public (bool Success, string Error) Verify(long userId)
        {
            try
            {
                var entity = this.DbContext.Users
                    .Include(u => u.AccountStatus)
                    .FirstOrDefault(u => u.UserId == userId);
                if (entity == null)
                {
                    return (false, ErrorMessages.UserNotFound);
                }

                var code = entity.AccountStatus?.Code;
                if (code != AccountStatusCodes.Unverified && code != AccountStatusCodes.Rejected)
                {
                    return (false, ErrorMessages.VerificationNotPending);
                }

                var active = this.DbContext.AccountStatuses.First(s => s.Code == AccountStatusCodes.Active);
                entity.AccountStatusId = active.AccountStatusId;
                entity.VerificationNote = null;
                entity.VerifiedAtUtc = DateTime.UtcNow;
                entity.VerifiedByUserId = this._currentUser.GetActorUserId();
                this._unitOfWork.SaveChanges();

                this.WriteAudit("VERIFY", "User", entity.UserId, entity.AccountIdentifier,
                    $"Verified account '{entity.FirstName} {entity.LastName}' ({entity.AccountIdentifier}).");

                this._notifications.Notify(entity.UserId, NotificationTypes.Account,
                    "Account verified",
                    "Your account has been verified. You can now submit equipment reservations.");

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

                var entity = this.DbContext.Users
                    .Include(u => u.AccountStatus)
                    .FirstOrDefault(u => u.UserId == userId);
                if (entity == null)
                {
                    return (false, ErrorMessages.UserNotFound);
                }

                if (entity.AccountStatus?.Code != AccountStatusCodes.Unverified)
                {
                    return (false, ErrorMessages.VerificationNotPending);
                }

                var rejected = this.DbContext.AccountStatuses.FirstOrDefault(s => s.Code == AccountStatusCodes.Rejected);
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
                    $"Your account could not be verified: {reason} You can submit an appeal from your dashboard.");

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
                var assignments = this.DbContext.CustodianAssignments
                    .Where(a => a.CustodianUserId == userId || a.BorrowerUserId == userId)
                    .ToList();
                this.DbContext.CustodianAssignments.RemoveRange(assignments);

                // Keep announcements and tickets; just detach them from the deleted account.
                foreach (var a in this.DbContext.Announcements.Where(a => a.CreatedByUserId == userId))
                {
                    a.CreatedByUserId = null;
                }
                foreach (var t in this.DbContext.SupportTickets.Where(t => t.AssignedToUserId == userId))
                {
                    t.AssignedToUserId = null;
                }

                this.DbContext.Users.Remove(entity);
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
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to delete user {UserId}.", userId);
                return (false, ErrorMessages.ServerError);
            }
        }

        public List<LookupViewModel> GetRoleOptions()
        {
            return this.DbContext.Roles
                .AsNoTracking()
                .OrderBy(r => r.RoleName)
                .Select(r => new LookupViewModel { Id = r.RoleId, Text = r.RoleName })
                .ToList();
        }

        public List<LookupViewModel> GetDepartmentOptions()
        {
            return this.DbContext.Departments
                .AsNoTracking()
                .Where(d => d.IsActive)
                .OrderBy(d => d.DepartmentName)
                .Select(d => new LookupViewModel { Id = d.DepartmentId, Text = d.DepartmentName })
                .ToList();
        }

        public List<LookupViewModel> GetAccountStatusOptions()
        {
            return this.DbContext.AccountStatuses
                .AsNoTracking()
                .OrderBy(s => s.Code)
                .Select(s => new LookupViewModel { Id = s.AccountStatusId, Text = s.Description })
                .ToList();
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

            this.DbContext.Users.Add(entity);
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

        private bool IsActiveAdmin(long roleId, long accountStatusId)
        {
            return this.DbContext.Roles.Any(r => r.RoleId == roleId && r.RoleCode == RoleCodes.Admin)
                && this.DbContext.AccountStatuses.Any(s => s.AccountStatusId == accountStatusId && s.Code == AccountStatusCodes.Active);
        }

        private bool OtherActiveAdminExists(long excludeUserId)
        {
            return this.DbContext.Users.Any(u => u.UserId != excludeUserId
                && u.Role.RoleCode == RoleCodes.Admin
                && u.AccountStatus.Code == AccountStatusCodes.Active);
        }

        private void WriteAudit(string actionType, string entityType, long entityId, string referenceNumber, string details)
        {
            this.DbContext.AuditLogs.Add(new AuditLog
            {
                ActorUserId = this._currentUser.GetActorUserId(),
                ActionType = actionType,
                EntityType = entityType,
                EntityId = entityId,
                ReferenceNumber = referenceNumber,
                Details = details,
                OccurredAtUtc = DateTime.UtcNow,
            });
            this._unitOfWork.SaveChanges();
        }
    }
}
