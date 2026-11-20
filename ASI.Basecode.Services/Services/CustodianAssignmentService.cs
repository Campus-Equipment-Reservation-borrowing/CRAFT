using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using ErrorMessages = ASI.Basecode.Resources.Messages.Errors;

namespace ASI.Basecode.Services.Services
{
    /// <inheritdoc cref="ICustodianAssignmentService" />
    public class CustodianAssignmentService : ServiceBase, ICustodianAssignmentService
    {
        private static readonly string[] OpenStatuses = { ReservationStatusCodes.Pending, ReservationStatusCodes.Approved, ReservationStatusCodes.ReadyForRelease, ReservationStatusCodes.PartiallyReleased };

        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditLogRepository _auditLogs;
        private readonly ICurrentUserService _currentUser;
        private readonly INotificationDispatcher _notifications;

        private readonly ICustodianAssignmentRepository _assignments;
        private readonly IUserRepository _users;
        private readonly IEquipmentRepository _equipment;
        private readonly IReservationRepository _reservations;

        public CustodianAssignmentService(
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            INotificationDispatcher notifications,
            IAuditLogRepository auditLogs,
            ICustodianAssignmentRepository assignments,
            IUserRepository users,
            IEquipmentRepository equipment,
            IReservationRepository reservations)
            : base(loggerFactory)
        {
            this._assignments = assignments;
            this._users = users;
            this._equipment = equipment;
            this._reservations = reservations;
            this._auditLogs = auditLogs;
            this._unitOfWork = unitOfWork;
            this._currentUser = currentUser;
            this._notifications = notifications;
        }

        public CustodianAssignmentIndexViewModel GetIndex()
        {
            var rows = this.LoadRows(this._assignments.GetAllWithDetails());
            var activeBorrowerIds = this._users.GetBorrowerIdsWithStatus(AccountStatusCodes.Active);
            foreach (var row in rows.Where(r => r.ScopeType == CustodianScopeTypes.Borrower))
            {
                row.RemovalLocked = activeBorrowerIds.Contains(row.TargetId);
            }

            var assignedBorrowers = rows.Where(r => r.ScopeType == CustodianScopeTypes.Borrower).Select(r => r.TargetId).ToHashSet();
            var assignedLocations = rows.Where(r => r.ScopeType == CustodianScopeTypes.Location).Select(r => r.TargetId).ToHashSet();

            return new CustodianAssignmentIndexViewModel
            {
                BorrowerAssignments = rows.Where(r => r.ScopeType == CustodianScopeTypes.Borrower).OrderBy(r => r.TargetName).ToList(),
                LocationAssignments = rows.Where(r => r.ScopeType == CustodianScopeTypes.Location).OrderBy(r => r.TargetName).ToList(),
                CustodianOptions = this.GetCustodianOptions(),
                UnassignedBorrowerOptions = this._users.GetBorrowersWithStatusByName(AccountStatusCodes.Active)
                    .Select(u => new { u.UserId, Text = u.LastName + ", " + u.FirstName + " (" + u.AccountIdentifier + ")" })
                    .Where(u => !assignedBorrowers.Contains(u.UserId))
                    .Select(u => new LookupViewModel { Id = u.UserId, Text = u.Text })
                    .ToList(),
                UnassignedLocationOptions = this._equipment.GetActiveLocationsByName()
                    .Select(l => new LookupViewModel { Id = l.LocationId, Text = l.LocationName })
                    .Where(l => !assignedLocations.Contains(l.Id))
                    .ToList(),
            };
        }

        public (bool Success, string Error) Assign(string scopeType, long custodianUserId, long targetId)
        {
            try
            {
                scopeType = scopeType?.Trim().ToUpperInvariant();
                if (targetId <= 0 || custodianUserId <= 0
                    || (scopeType != CustodianScopeTypes.Borrower && scopeType != CustodianScopeTypes.Location))
                {
                    return (false, ErrorMessages.AssignmentInvalid);
                }

                if (scopeType == CustodianScopeTypes.Borrower)
                {
                    return this.AssignBorrower(targetId, custodianUserId);
                }

                var custodianUser = this._users.GetInRoleWithStatus(custodianUserId, RoleCodes.Technician, AccountStatusCodes.Active);
                var custodian = custodianUser == null ? null : new { custodianUser.UserId, Name = custodianUser.FirstName + " " + custodianUser.LastName };
                if (custodian == null)
                {
                    return (false, ErrorMessages.AssignmentInvalid);
                }

                var assignment = new CustodianAssignment
                {
                    CustodianUserId = custodian.UserId,
                    ScopeType = scopeType,
                    AssignedByUserId = this._currentUser.GetActorUserId(),
                    AssignedAtUtc = DateTime.UtcNow,
                };

                var targetName = this._equipment.GetLocationName(targetId);
                if (this._assignments.LocationHasAssignment(targetId))
                {
                    return (false, ErrorMessages.AssignmentDuplicate);
                }

                assignment.LocationId = targetId;

                if (targetName == null)
                {
                    return (false, ErrorMessages.AssignmentInvalid);
                }

                this._assignments.Add(assignment);
                this._unitOfWork.SaveChanges();

                this.WriteAudit("ASSIGN", assignment.AssignmentId,
                    $"Assigned {scopeType.ToLowerInvariant()} '{targetName}' to custodian {custodian.Name}.");

                this._notifications.Notify(custodian.UserId, NotificationTypes.Account,
                    "New custodian assignment",
                    $"You are now the custodian for {scopeType.ToLowerInvariant()} '{targetName}'. New reservations for it will be routed to you.");

                return (true, null);
            }
            catch (DbUpdateException ex)
            {
                // The filtered unique indexes catch a concurrent duplicate.
                this._logger.LogWarning(ex, "Assignment rejected by the database.");
                return (false, ErrorMessages.AssignmentDuplicate);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to assign {Scope} {TargetId} to custodian {CustodianId}.", scopeType, targetId, custodianUserId);
                return (false, ErrorMessages.ServerError);
            }
        }

        public (bool Success, string Error) Unassign(long assignmentId)
        {
            try
            {
                var found = this._assignments.GetWithDetailsById(assignmentId);
                var row = found == null ? null : this.LoadRows(new List<CustodianAssignment> { found }).FirstOrDefault();
                var entity = this._assignments.GetTrackedById(assignmentId);
                if (entity == null || row == null)
                {
                    return (false, ErrorMessages.AssignmentNotFound);
                }

                if (entity.BorrowerUserId.HasValue
                    && this._users.HasAccountStatus(entity.BorrowerUserId.Value, AccountStatusCodes.Active))
                {
                    return (false, ErrorMessages.AssignmentBorrowerRequired);
                }

                this._assignments.Remove(entity);
                this._unitOfWork.SaveChanges();

                this.WriteAudit("UNASSIGN", assignmentId,
                    $"Removed {row.ScopeType.ToLowerInvariant()} '{row.TargetName}' from custodian {row.CustodianName}.");

                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to remove assignment {AssignmentId}.", assignmentId);
                return (false, ErrorMessages.ServerError);
            }
        }

        public long? ResolveCustodian(long borrowerUserId, long equipmentId)
        {
            var (locationId, _) = this._equipment.GetLocationAndCategory(equipmentId);

            // Only active custodians receive routed work; otherwise it falls through to the pool.
            var candidates = this._assignments.GetRoutingCandidates(borrowerUserId, locationId, AccountStatusCodes.Active);

            long? First(string scopeType) => candidates.Where(c => c.ScopeType == scopeType).Select(c => (long?)c.CustodianUserId).FirstOrDefault();
            return First(CustodianScopeTypes.Borrower)
                ?? First(CustodianScopeTypes.Location);
        }

        public UserAssignmentSummaryViewModel GetUserSummary(long userId)
        {
            var userEntity = this._users.GetByIdWithDetails(userId);
            var user = userEntity == null ? null : new { userEntity.Role.RoleCode, userEntity.Role.IsBorrowerRole };
            if (user == null)
            {
                return new UserAssignmentSummaryViewModel();
            }

            var summary = new UserAssignmentSummaryViewModel
            {
                IsCustodian = user.RoleCode == RoleCodes.Technician,
                IsBorrower = user.IsBorrowerRole,
            };

            if (summary.IsCustodian)
            {
                summary.Assignments = this.LoadRows(this._assignments.GetWithDetailsForCustodian(userId))
                    .OrderBy(r => r.ScopeType).ThenBy(r => r.TargetName).ToList();
            }

            if (summary.IsBorrower)
            {
                var directEntity = this._assignments.GetForBorrowerWithCustodian(userId);
                var direct = directEntity == null ? null : new { directEntity.CustodianUserId, Name = directEntity.CustodianUser.FirstName + " " + directEntity.CustodianUser.LastName };
                summary.AssignedCustodianUserId = direct?.CustodianUserId;
                summary.AssignedCustodianName = direct?.Name;
            }

            return summary;
        }

        public CustodianWorkloadViewModel GetWorkload()
        {
            var open = this._reservations.GetInStatusesWithDetails(OpenStatuses)
                .Select(r => new OpenReservationRowViewModel
                {
                    ReservationId = r.ReservationId,
                    ReservationNumber = r.ReservationNumber,
                    BorrowerName = r.BorrowerUser.FirstName + " " + r.BorrowerUser.LastName,
                    EquipmentName = r.Equipment.EquipmentName,
                    StatusCode = r.Status.Code,
                    StatusDescription = r.Status.Description,
                    AssignedCustodianUserId = r.AssignedCustodianUserId,
                    SubmittedAtUtc = r.SubmittedAtUtc,
                })
                .ToList();

            var custodians = this._users.GetInRoleByName(RoleCodes.Technician)
                .Select(u => new { u.UserId, Name = u.FirstName + " " + u.LastName, Active = u.AccountStatus?.Code == AccountStatusCodes.Active })
                .ToList();
            var assignmentCounts = this._assignments.CountByCustodian();

            return new CustodianWorkloadViewModel
            {
                Rows = custodians.Select(c => new CustodianWorkloadRowViewModel
                {
                    CustodianUserId = c.UserId,
                    CustodianName = c.Active ? c.Name : c.Name + " (inactive)",
                    Pending = open.Count(r => r.AssignedCustodianUserId == c.UserId && r.StatusCode == ReservationStatusCodes.Pending),
                    Approved = open.Count(r => r.AssignedCustodianUserId == c.UserId && r.StatusCode != ReservationStatusCodes.Pending),
                    AssignmentCount = assignmentCounts.TryGetValue(c.UserId, out var n) ? n : 0,
                }).ToList(),
                PoolPending = open.Count(r => r.AssignedCustodianUserId == null && r.StatusCode == ReservationStatusCodes.Pending),
                PoolApproved = open.Count(r => r.AssignedCustodianUserId == null && r.StatusCode != ReservationStatusCodes.Pending),
                OpenReservations = open.OrderBy(r => r.SubmittedAtUtc).Take(15).ToList(),
                CustodianOptions = this.GetCustodianOptions(),
            };
        }

        public (bool Success, string Error) Reassign(long reservationId, long? custodianUserId)
        {
            try
            {
                var reservation = this._reservations.GetTrackedWithStatus(reservationId);
                if (reservation == null)
                {
                    return (false, ErrorMessages.CustodianReservationNotFound);
                }

                if (!OpenStatuses.Contains(reservation.Status.Code))
                {
                    return (false, ErrorMessages.CustodianClaimUnavailable);
                }

                string custodianName = null;
                if (custodianUserId.HasValue)
                {
                    var custodianUser = this._users.GetInRoleWithStatus(custodianUserId.Value, RoleCodes.Technician, AccountStatusCodes.Active);
                    custodianName = custodianUser == null ? null : custodianUser.FirstName + " " + custodianUser.LastName;
                    if (custodianName == null)
                    {
                        return (false, ErrorMessages.AssignmentInvalid);
                    }
                }

                reservation.AssignedCustodianUserId = custodianUserId;
                this._unitOfWork.SaveChanges();

                this.WriteAudit("REASSIGN", reservation.ReservationId,
                    custodianName == null
                        ? $"Returned reservation {reservation.ReservationNumber} to the shared pool."
                        : $"Reassigned reservation {reservation.ReservationNumber} to {custodianName}.",
                    "Reservation", reservation.ReservationNumber);

                if (custodianUserId.HasValue)
                {
                    this._notifications.Notify(custodianUserId.Value, NotificationTypes.Reservation,
                        $"Reservation {reservation.ReservationNumber} assigned to you",
                        $"An administrator assigned reservation {reservation.ReservationNumber} to you. Review it in the custodian console.",
                        $"/Custodian/Review/{reservation.ReservationId}");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to reassign reservation {ReservationId}.", reservationId);
                return (false, ErrorMessages.ServerError);
            }
        }

        /// <summary>Borrower scope from the assignments screen: only for verified borrowers; changes the custodian if one exists.</summary>
        private (bool Success, string Error) AssignBorrower(long borrowerUserId, long custodianUserId)
        {
            var borrowerActive = this._users.IsBorrowerWithStatus(borrowerUserId, AccountStatusCodes.Active);
            if (!borrowerActive)
            {
                return (false, ErrorMessages.AssignmentBorrowerNotActive);
            }

            using var transaction = this._unitOfWork.BeginTransaction();
            var staged = this.StageBorrowerAssignment(borrowerUserId, custodianUserId);
            if (!staged.Success)
            {
                return (false, staged.Error);
            }

            this._unitOfWork.SaveChanges();
            this.WriteAudit("ASSIGN", staged.Assignment.AssignmentId, staged.Details);
            transaction.Commit();

            this._notifications.Notify(custodianUserId, NotificationTypes.Account,
                "New custodian assignment",
                $"You are now the custodian for borrower '{staged.BorrowerName}'. Their reservations will be routed to you.");
            return (true, null);
        }

        /// <inheritdoc />
        public BorrowerAssignmentStage StageBorrowerAssignment(long borrowerUserId, long custodianUserId)
        {
            var custodianUser = this._users.GetInRoleWithStatus(custodianUserId, RoleCodes.Technician, AccountStatusCodes.Active);
            var custodian = custodianUser == null ? null : new { custodianUser.UserId, Name = custodianUser.FirstName + " " + custodianUser.LastName };
            if (custodian == null)
            {
                return BorrowerAssignmentStage.Fail(ErrorMessages.VerificationCustodianRequired);
            }

            var borrowerUser = this._users.GetBorrower(borrowerUserId);
            var borrower = borrowerUser == null ? null : new { borrowerUser.UserId, Name = borrowerUser.FirstName + " " + borrowerUser.LastName };
            if (borrower == null)
            {
                return BorrowerAssignmentStage.Fail(ErrorMessages.AssignmentInvalid);
            }

            var assignment = this._assignments.GetTrackedForBorrower(borrowerUserId);
            var previousCustodianId = assignment?.CustodianUserId;
            if (assignment == null)
            {
                assignment = new CustodianAssignment
                {
                    ScopeType = CustodianScopeTypes.Borrower,
                    BorrowerUserId = borrowerUserId,
                };
                this._assignments.Add(assignment);
            }

            assignment.CustodianUserId = custodian.UserId;
            assignment.AssignedByUserId = this._currentUser.GetActorUserId();
            assignment.AssignedAtUtc = DateTime.UtcNow;

            // Open reservations still sitting in the pool (or with the previous custodian) follow the borrower.
            var openReservations = this._reservations.GetTrackedForBorrowerInStatusesUnassignedOrWith(borrowerUserId, OpenStatuses, previousCustodianId);
            foreach (var reservation in openReservations)
            {
                reservation.AssignedCustodianUserId = custodian.UserId;
            }

            var details = previousCustodianId.HasValue && previousCustodianId != custodian.UserId
                ? $"Moved borrower '{borrower.Name}' to custodian {custodian.Name}; {openReservations.Count} open reservation(s) rerouted."
                : $"Assigned borrower '{borrower.Name}' to custodian {custodian.Name}; {openReservations.Count} open reservation(s) routed.";

            return new BorrowerAssignmentStage
            {
                Success = true,
                Assignment = assignment,
                BorrowerName = borrower.Name,
                CustodianName = custodian.Name,
                Details = details,
            };
        }

        /// <inheritdoc />
        public List<LookupViewModel> GetCustodianOptions()
        {
            return this._users.GetInRoleByName(RoleCodes.Technician, AccountStatusCodes.Active)
                .Select(u => new LookupViewModel { Id = u.UserId, Text = u.FirstName + " " + u.LastName })
                .ToList();
        }

        private List<CustodianAssignmentRowViewModel> LoadRows(IEnumerable<CustodianAssignment> assignments)
        {
            return assignments
                .Select(a => new CustodianAssignmentRowViewModel
                {
                    AssignmentId = a.AssignmentId,
                    ScopeType = a.ScopeType,
                    CustodianUserId = a.CustodianUserId,
                    CustodianName = a.CustodianUser?.FirstName + " " + a.CustodianUser?.LastName,
                    TargetId = a.BorrowerUserId ?? a.LocationId ?? a.EquipmentCategoryId ?? 0,
                    TargetName = a.BorrowerUser != null
                        ? a.BorrowerUser.FirstName + " " + a.BorrowerUser.LastName
                        : a.Location != null ? a.Location.LocationName : a.EquipmentCategory?.CategoryName,
                    TargetDetail = a.BorrowerUser != null
                        ? a.BorrowerUser.AccountIdentifier
                        : a.Location != null ? a.Location.LocationCode : a.EquipmentCategory?.CategoryCode,
                    AssignedAtUtc = a.AssignedAtUtc,
                })
                .ToList();
        }

        private void WriteAudit(string actionType, long entityId, string details, string entityType = "CustodianAssignment", string referenceNumber = null)
        {
            this._auditLogs.Add(new AuditLog
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
