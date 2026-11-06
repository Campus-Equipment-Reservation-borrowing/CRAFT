using ASI.Basecode.Data;
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
        private static readonly string[] OpenStatuses = { "PENDING", "APPROVED", "READY_FOR_RELEASE", "PARTIALLY_RELEASED" };

        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly INotificationDispatcher _notifications;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public CustodianAssignmentService(
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            INotificationDispatcher notifications)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
            this._currentUser = currentUser;
            this._notifications = notifications;
        }

        public CustodianAssignmentIndexViewModel GetIndex()
        {
            var rows = this.LoadRows(this.DbContext.CustodianAssignments.AsNoTracking());

            var assignedBorrowers = rows.Where(r => r.ScopeType == CustodianScopeTypes.Borrower).Select(r => r.TargetId).ToHashSet();
            var assignedLocations = rows.Where(r => r.ScopeType == CustodianScopeTypes.Location).Select(r => r.TargetId).ToHashSet();
            var assignedCategories = rows.Where(r => r.ScopeType == CustodianScopeTypes.Category).Select(r => r.TargetId).ToHashSet();

            return new CustodianAssignmentIndexViewModel
            {
                BorrowerAssignments = rows.Where(r => r.ScopeType == CustodianScopeTypes.Borrower).OrderBy(r => r.TargetName).ToList(),
                LocationAssignments = rows.Where(r => r.ScopeType == CustodianScopeTypes.Location).OrderBy(r => r.TargetName).ToList(),
                CategoryAssignments = rows.Where(r => r.ScopeType == CustodianScopeTypes.Category).OrderBy(r => r.TargetName).ToList(),
                CustodianOptions = this.GetCustodianOptions(),
                UnassignedBorrowerOptions = this.DbContext.Users.AsNoTracking()
                    .Where(u => u.Role.IsBorrowerRole)
                    .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                    .Select(u => new { u.UserId, Text = u.LastName + ", " + u.FirstName + " (" + u.AccountIdentifier + ")" })
                    .ToList()
                    .Where(u => !assignedBorrowers.Contains(u.UserId))
                    .Select(u => new LookupViewModel { Id = u.UserId, Text = u.Text })
                    .ToList(),
                UnassignedLocationOptions = this.DbContext.Locations.AsNoTracking()
                    .Where(l => l.IsActive)
                    .OrderBy(l => l.LocationName)
                    .Select(l => new LookupViewModel { Id = l.LocationId, Text = l.LocationName })
                    .ToList()
                    .Where(l => !assignedLocations.Contains(l.Id))
                    .ToList(),
                UnassignedCategoryOptions = this.DbContext.EquipmentCategories.AsNoTracking()
                    .OrderBy(c => c.CategoryName)
                    .Select(c => new LookupViewModel { Id = c.EquipmentCategoryId, Text = c.CategoryName })
                    .ToList()
                    .Where(c => !assignedCategories.Contains(c.Id))
                    .ToList(),
            };
        }

        public (bool Success, string Error) Assign(string scopeType, long custodianUserId, long targetId)
        {
            try
            {
                scopeType = scopeType?.Trim().ToUpperInvariant();
                if (targetId <= 0 || custodianUserId <= 0
                    || (scopeType != CustodianScopeTypes.Borrower && scopeType != CustodianScopeTypes.Location && scopeType != CustodianScopeTypes.Category))
                {
                    return (false, ErrorMessages.AssignmentInvalid);
                }

                var custodian = this.DbContext.Users.AsNoTracking()
                    .Where(u => u.UserId == custodianUserId && u.Role.RoleCode == RoleCodes.Technician && u.AccountStatus.Code == AccountStatusCodes.Active)
                    .Select(u => new { u.UserId, Name = u.FirstName + " " + u.LastName })
                    .FirstOrDefault();
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

                string targetName;
                switch (scopeType)
                {
                    case CustodianScopeTypes.Borrower:
                        targetName = this.DbContext.Users.AsNoTracking()
                            .Where(u => u.UserId == targetId && u.Role.IsBorrowerRole)
                            .Select(u => u.FirstName + " " + u.LastName).FirstOrDefault();
                        if (this.DbContext.CustodianAssignments.Any(a => a.BorrowerUserId == targetId))
                        {
                            return (false, ErrorMessages.AssignmentDuplicate);
                        }

                        assignment.BorrowerUserId = targetId;
                        break;
                    case CustodianScopeTypes.Location:
                        targetName = this.DbContext.Locations.AsNoTracking()
                            .Where(l => l.LocationId == targetId).Select(l => l.LocationName).FirstOrDefault();
                        if (this.DbContext.CustodianAssignments.Any(a => a.LocationId == targetId))
                        {
                            return (false, ErrorMessages.AssignmentDuplicate);
                        }

                        assignment.LocationId = targetId;
                        break;
                    default:
                        targetName = this.DbContext.EquipmentCategories.AsNoTracking()
                            .Where(c => c.EquipmentCategoryId == targetId).Select(c => c.CategoryName).FirstOrDefault();
                        if (this.DbContext.CustodianAssignments.Any(a => a.EquipmentCategoryId == targetId))
                        {
                            return (false, ErrorMessages.AssignmentDuplicate);
                        }

                        assignment.EquipmentCategoryId = targetId;
                        break;
                }

                if (targetName == null)
                {
                    return (false, ErrorMessages.AssignmentInvalid);
                }

                this.DbContext.CustodianAssignments.Add(assignment);
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
                var row = this.LoadRows(this.DbContext.CustodianAssignments.AsNoTracking().Where(a => a.AssignmentId == assignmentId)).FirstOrDefault();
                var entity = this.DbContext.CustodianAssignments.FirstOrDefault(a => a.AssignmentId == assignmentId);
                if (entity == null || row == null)
                {
                    return (false, ErrorMessages.AssignmentNotFound);
                }

                this.DbContext.CustodianAssignments.Remove(entity);
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
            var equipment = this.DbContext.Equipment.AsNoTracking()
                .Where(e => e.EquipmentId == equipmentId)
                .Select(e => new { e.LocationId, CategoryId = (long?)e.EquipmentCategoryId })
                .FirstOrDefault();
            var locationId = equipment?.LocationId;
            var categoryId = equipment?.CategoryId;

            // Only active custodians receive routed work; otherwise it falls through to the pool.
            var candidates = this.DbContext.CustodianAssignments.AsNoTracking()
                .Where(a => a.CustodianUser.AccountStatus.Code == AccountStatusCodes.Active
                    && (a.BorrowerUserId == borrowerUserId
                        || (locationId != null && a.LocationId == locationId)
                        || (categoryId != null && a.EquipmentCategoryId == categoryId)))
                .Select(a => new { a.ScopeType, a.CustodianUserId })
                .ToList();

            return candidates.FirstOrDefault(c => c.ScopeType == CustodianScopeTypes.Borrower)?.CustodianUserId
                ?? candidates.FirstOrDefault(c => c.ScopeType == CustodianScopeTypes.Location)?.CustodianUserId
                ?? candidates.FirstOrDefault(c => c.ScopeType == CustodianScopeTypes.Category)?.CustodianUserId;
        }

        public UserAssignmentSummaryViewModel GetUserSummary(long userId)
        {
            var user = this.DbContext.Users.AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new { u.Role.RoleCode, u.Role.IsBorrowerRole })
                .FirstOrDefault();
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
                summary.Assignments = this.LoadRows(this.DbContext.CustodianAssignments.AsNoTracking().Where(a => a.CustodianUserId == userId))
                    .OrderBy(r => r.ScopeType).ThenBy(r => r.TargetName).ToList();
            }

            if (summary.IsBorrower)
            {
                var direct = this.DbContext.CustodianAssignments.AsNoTracking()
                    .Where(a => a.BorrowerUserId == userId)
                    .Select(a => new { a.CustodianUserId, Name = a.CustodianUser.FirstName + " " + a.CustodianUser.LastName })
                    .FirstOrDefault();
                summary.AssignedCustodianUserId = direct?.CustodianUserId;
                summary.AssignedCustodianName = direct?.Name;
            }

            return summary;
        }

        public CustodianWorkloadViewModel GetWorkload()
        {
            var open = this.DbContext.Reservations.AsNoTracking()
                .Where(r => OpenStatuses.Contains(r.Status.Code))
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

            var custodians = this.DbContext.Users.AsNoTracking()
                .Where(u => u.Role.RoleCode == RoleCodes.Technician)
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                .Select(u => new { u.UserId, Name = u.FirstName + " " + u.LastName, Active = u.AccountStatus.Code == AccountStatusCodes.Active })
                .ToList();
            var assignmentCounts = this.DbContext.CustodianAssignments.AsNoTracking()
                .GroupBy(a => a.CustodianUserId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionary(x => x.Key, x => x.Count);

            return new CustodianWorkloadViewModel
            {
                Rows = custodians.Select(c => new CustodianWorkloadRowViewModel
                {
                    CustodianUserId = c.UserId,
                    CustodianName = c.Active ? c.Name : c.Name + " (inactive)",
                    Pending = open.Count(r => r.AssignedCustodianUserId == c.UserId && r.StatusCode == "PENDING"),
                    Approved = open.Count(r => r.AssignedCustodianUserId == c.UserId && r.StatusCode != "PENDING"),
                    AssignmentCount = assignmentCounts.TryGetValue(c.UserId, out var n) ? n : 0,
                }).ToList(),
                PoolPending = open.Count(r => r.AssignedCustodianUserId == null && r.StatusCode == "PENDING"),
                PoolApproved = open.Count(r => r.AssignedCustodianUserId == null && r.StatusCode != "PENDING"),
                OpenReservations = open.OrderBy(r => r.SubmittedAtUtc).Take(15).ToList(),
                CustodianOptions = this.GetCustodianOptions(),
            };
        }

        public (bool Success, string Error) Reassign(long reservationId, long? custodianUserId)
        {
            try
            {
                var reservation = this.DbContext.Reservations
                    .Include(r => r.Status)
                    .FirstOrDefault(r => r.ReservationId == reservationId);
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
                    custodianName = this.DbContext.Users.AsNoTracking()
                        .Where(u => u.UserId == custodianUserId.Value && u.Role.RoleCode == RoleCodes.Technician && u.AccountStatus.Code == AccountStatusCodes.Active)
                        .Select(u => u.FirstName + " " + u.LastName)
                        .FirstOrDefault();
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
                        $"An administrator assigned reservation {reservation.ReservationNumber} to you. Review it in the custodian console.");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to reassign reservation {ReservationId}.", reservationId);
                return (false, ErrorMessages.ServerError);
            }
        }

        private List<LookupViewModel> GetCustodianOptions()
        {
            return this.DbContext.Users.AsNoTracking()
                .Where(u => u.Role.RoleCode == RoleCodes.Technician && u.AccountStatus.Code == AccountStatusCodes.Active)
                .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                .Select(u => new LookupViewModel { Id = u.UserId, Text = u.FirstName + " " + u.LastName })
                .ToList();
        }

        private List<CustodianAssignmentRowViewModel> LoadRows(IQueryable<CustodianAssignment> query)
        {
            return query
                .Select(a => new CustodianAssignmentRowViewModel
                {
                    AssignmentId = a.AssignmentId,
                    ScopeType = a.ScopeType,
                    CustodianUserId = a.CustodianUserId,
                    CustodianName = a.CustodianUser.FirstName + " " + a.CustodianUser.LastName,
                    TargetId = a.BorrowerUserId ?? a.LocationId ?? a.EquipmentCategoryId ?? 0,
                    TargetName = a.BorrowerUser != null
                        ? a.BorrowerUser.FirstName + " " + a.BorrowerUser.LastName
                        : a.Location != null ? a.Location.LocationName : a.EquipmentCategory.CategoryName,
                    TargetDetail = a.BorrowerUser != null
                        ? a.BorrowerUser.AccountIdentifier
                        : a.Location != null ? a.Location.LocationCode : a.EquipmentCategory.CategoryCode,
                    AssignedAtUtc = a.AssignedAtUtc,
                })
                .ToList();
        }

        private void WriteAudit(string actionType, long entityId, string details, string entityType = "CustodianAssignment", string referenceNumber = null)
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
