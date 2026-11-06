using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using ErrorMessages = ASI.Basecode.Resources.Messages.Errors;

namespace ASI.Basecode.Services.Services
{
    public class CustodianService : ServiceBase, ICustodianService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICustodianRepository _repository;
        private readonly IReservationRepository _reservationRepository;
        private readonly ICurrentUserService _currentUser;
        private readonly INotificationDispatcher _notifications;
        private readonly IMapper _mapper;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public CustodianService(
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            ICustodianRepository repository,
            IReservationRepository reservationRepository,
            ICurrentUserService currentUser,
            INotificationDispatcher notifications,
            IMapper mapper)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
            this._repository = repository;
            this._reservationRepository = reservationRepository;
            this._currentUser = currentUser;
            this._notifications = notifications;
            this._mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public CustodianReservationsIndexViewModel GetReservationsIndex(string search, long? statusId, int page, int pageSize)
        {
            var result = this._repository.GetReservationsPaged(search, statusId, page, pageSize, this._currentUser.GetCustodianScope());
            return new CustodianReservationsIndexViewModel
            {
                Page = new PagedResult<CustodianReservationRowViewModel>
                {
                    Items = result.Items.Select(this.MapReservationRow).ToList(),
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount,
                },
                Search = search,
                StatusId = statusId,
                StatusOptions = this.DbContext.ReservationStatuses.AsNoTracking()
                    .OrderBy(s => s.StatusId).Select(s => new LookupViewModel { Id = s.StatusId, Text = s.Description }).ToList(),
            };
        }

        public ReservationReviewViewModel GetReservationReview(long reservationId)
        {
            var entity = this._repository.GetReservationWithDetails(reservationId);
            if (entity == null || !this._currentUser.CanActOn(entity.AssignedCustodianUserId))
            {
                return null;
            }

            return new ReservationReviewViewModel
            {
                AssignedCustodianName = entity.AssignedCustodianUser == null ? null : entity.AssignedCustodianUser.FirstName + " " + entity.AssignedCustodianUser.LastName,
                CanClaim = entity.AssignedCustodianUserId == null && (entity.Status.Code == "PENDING" || entity.Status.Code == "APPROVED"),
                ReservationId = entity.ReservationId,
                ReservationNumber = entity.ReservationNumber,
                StatusCode = entity.Status.Code,
                StatusDescription = entity.Status.Description,
                BorrowerName = entity.BorrowerUser.FirstName + " " + entity.BorrowerUser.LastName,
                BorrowerRole = entity.BorrowerUser.Role.RoleName,
                BorrowerDepartment = entity.BorrowerUser.Department?.DepartmentName,
                BorrowerEmail = entity.BorrowerUser.SchoolEmail,
                EquipmentName = entity.Equipment.EquipmentName,
                AssetCode = entity.Equipment.AssetCode,
                CategoryName = entity.Equipment.EquipmentCategory.CategoryName,
                LocationName = entity.Equipment.Location?.LocationName,
                PickupLocationName = entity.PickupLocation?.LocationName,
                RequestedQuantity = entity.RequestedQuantity,
                ApprovedQuantity = entity.ApprovedQuantity,
                HeldQuantity = entity.HeldQuantity,
                RequestedStartAtUtc = entity.RequestedStartAtUtc,
                ExpectedReturnAtUtc = entity.ExpectedReturnAtUtc,
                Purpose = entity.Purpose,
                SubmittedAtUtc = entity.SubmittedAtUtc,
                CanApprove = entity.Status.Code == "PENDING",
                CanReject = entity.Status.Code == "PENDING",
            };
        }

        public (bool Success, string Error) Approve(long reservationId, string notes)
        {
            try
            {
                var entity = this._repository.GetReservationWithDetails(reservationId);
                if (entity == null)
                {
                    return (false, ErrorMessages.CustodianReservationNotFound);
                }
                if (!this._currentUser.CanActOn(entity.AssignedCustodianUserId))
                {
                    return (false, ErrorMessages.CustodianNotAssigned);
                }
                if (entity.Status.Code != "PENDING")
                {
                    return (false, ErrorMessages.CustodianReservationNotPending);
                }
                this.ClaimIfPooled(entity);

                var approvedStatus = this.DbContext.ReservationStatuses.FirstOrDefault(s => s.Code == "APPROVED");
                if (approvedStatus == null)
                {
                    return (false, ErrorMessages.ServerError);
                }

                entity.StatusId = approvedStatus.StatusId;
                entity.ApprovedQuantity = entity.RequestedQuantity;
                entity.HeldQuantity = entity.RequestedQuantity;

                // The units were held on the balance at submit time (ReservationService.Create).

                this._unitOfWork.SaveChanges();

                this.WriteNotification(entity.BorrowerUserId, "Reservation approved",
                    $"Your reservation {entity.ReservationNumber} for {entity.Equipment.EquipmentName} has been approved. Please pick up at {entity.PickupLocation?.LocationName ?? "the designated location"}.");

                this.WriteAudit("APPROVE", "Reservation", entity.ReservationId, entity.ReservationNumber,
                    $"Approved reservation {entity.ReservationNumber} for {entity.RequestedQuantity} unit(s) of {entity.Equipment.AssetCode}." +
                    (string.IsNullOrWhiteSpace(notes) ? string.Empty : " Notes: " + notes.Trim()));

                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to approve reservation {ReservationId}.", reservationId);
                return (false, ErrorMessages.ServerError);
            }
        }

        public (bool Success, string Error) Reject(long reservationId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return (false, ErrorMessages.CustodianRejectReasonRequired);
            }

            try
            {
                var entity = this._repository.GetReservationWithDetails(reservationId);
                if (entity == null)
                {
                    return (false, ErrorMessages.CustodianReservationNotFound);
                }
                if (!this._currentUser.CanActOn(entity.AssignedCustodianUserId))
                {
                    return (false, ErrorMessages.CustodianNotAssigned);
                }
                if (entity.Status.Code != "PENDING")
                {
                    return (false, ErrorMessages.CustodianReservationNotPending);
                }
                this.ClaimIfPooled(entity);

                var rejectedStatus = this.DbContext.ReservationStatuses.FirstOrDefault(s => s.Code == "REJECTED");
                if (rejectedStatus == null)
                {
                    return (false, ErrorMessages.ServerError);
                }

                var heldUnits = this.RemainingHold(entity);
                entity.StatusId = rejectedStatus.StatusId;
                ReleaseHold(entity, heldUnits);
                entity.HeldQuantity = 0;
                this._unitOfWork.SaveChanges();

                this.WriteNotification(entity.BorrowerUserId, "Reservation rejected",
                    $"Your reservation {entity.ReservationNumber} for {entity.Equipment.EquipmentName} was rejected. Reason: {reason.Trim()}");

                this.WriteAudit("REJECT", "Reservation", entity.ReservationId, entity.ReservationNumber,
                    $"Rejected reservation {entity.ReservationNumber}. Reason: {reason.Trim()}");

                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to reject reservation {ReservationId}.", reservationId);
                return (false, ErrorMessages.ServerError);
            }
        }

        public ReleaseContextViewModel GetReleaseContext(long reservationId)
        {
            var entity = this._repository.GetReservationWithDetails(reservationId);
            if (entity == null || !this._currentUser.CanActOn(entity.AssignedCustodianUserId))
            {
                return null;
            }

            return new ReleaseContextViewModel
            {
                ReservationId = entity.ReservationId,
                ReservationNumber = entity.ReservationNumber,
                BorrowerName = entity.BorrowerUser.FirstName + " " + entity.BorrowerUser.LastName,
                BorrowerRole = entity.BorrowerUser.Role.RoleName,
                EquipmentName = entity.Equipment.EquipmentName,
                AssetCode = entity.Equipment.AssetCode,
                CategoryName = entity.Equipment.EquipmentCategory.CategoryName,
                LocationName = entity.Equipment.Location?.LocationName,
                PickupLocationName = entity.PickupLocation?.LocationName,
                ApprovedQuantity = this.RemainingHold(entity),
                AlreadyReleasedQuantity = this._repository.SumReleasedForReservation(reservationId),
                ExpectedReturnAtUtc = entity.ExpectedReturnAtUtc,
                CanRelease = (entity.Status.Code == "APPROVED" || entity.Status.Code == "PARTIALLY_RELEASED") &&
                             this.RemainingHold(entity) > 0,
                ConditionOptions = this.DbContext.Conditions.AsNoTracking()
                    .OrderBy(c => c.Code).Select(c => new LookupViewModel { Id = c.ConditionId, Text = c.Description }).ToList(),
                ActiveBorrowingExists = this._repository.CountActiveBorrowingsForReservation(reservationId) > 0,
            };
        }

        public (bool Success, string Error, long BorrowingId) Release(ReleaseFormViewModel form)
        {
            if (form == null || form.ReleasedQuantity < 1 || !form.ReleaseConditionId.HasValue || form.ReleaseConditionId.Value < 1)
            {
                return (false, ErrorMessages.CustodianReleaseQuantityInvalid, 0);
            }

            try
            {
                var entity = this._repository.GetReservationWithDetails(form.ReservationId);
                if (entity == null)
                {
                    return (false, ErrorMessages.CustodianReservationNotFound, 0);
                }
                if (!this._currentUser.CanActOn(entity.AssignedCustodianUserId))
                {
                    return (false, ErrorMessages.CustodianNotAssigned, 0);
                }
                if (entity.Status.Code != "APPROVED" && entity.Status.Code != "PARTIALLY_RELEASED")
                {
                    return (false, ErrorMessages.CustodianReleaseNotApproved, 0);
                }
                this.ClaimIfPooled(entity);

                var maxQty = this.RemainingHold(entity);
                if (maxQty < 1)
                {
                    return (false, ErrorMessages.CustodianReleaseNotApproved, 0);
                }
                if (form.ReleasedQuantity > maxQty)
                {
                    return (false, ErrorMessages.CustodianReleaseQuantityInvalid, 0);
                }

                var policyId = entity.BorrowingPolicyVersionId;
                if (policyId == 0)
                {
                    var latest = this.DbContext.BorrowingPolicyVersions
                        .OrderByDescending(p => p.IsCurrent).ThenByDescending(p => p.PolicyVersionId).FirstOrDefault();
                    policyId = latest?.PolicyVersionId ?? 0;
                }

                var txn = this._repository.GetNextTransactionNumber(DateTime.UtcNow.Year);

                var borrowingStatus = this.DbContext.BorrowingStatuses.FirstOrDefault(s => s.Code == "ACTIVE");
                var inspectionStatus = this.DbContext.InspectionStatuses.FirstOrDefault(s => s.Code == "PENDING");
                if (borrowingStatus == null || inspectionStatus == null)
                {
                    return (false, ErrorMessages.ServerError, 0);
                }

                var borrowing = new Borrowing
                {
                    TransactionNumber = txn,
                    ReservationId = entity.ReservationId,
                    BorrowerUserId = entity.BorrowerUserId,
                    EquipmentId = entity.EquipmentId,
                    BorrowingPolicyVersionId = policyId,
                    ReleasedQuantity = form.ReleasedQuantity,
                    ReleaseConditionId = form.ReleaseConditionId.Value,
                    ReleasedAtUtc = DateTime.UtcNow,
                    ReleasedByUserId = this._currentUser.GetActorUserId() ?? 0,
                    DueAtUtc = entity.ExpectedReturnAtUtc,
                    StatusId = borrowingStatus.StatusId,
                    InspectionStatusId = inspectionStatus.StatusId,
                };

                if (borrowing.ReleasedByUserId == 0)
                {
                    // fall back to the first custodian/admin in the DB so the FK holds
                    var fallback = this.DbContext.Users
                        .Where(u => u.Role.RoleCode == "TECHNICIAN" || u.Role.RoleCode == "ADMIN")
                        .OrderBy(u => u.UserId).FirstOrDefault();
                    if (fallback != null)
                    {
                        borrowing.ReleasedByUserId = fallback.UserId;
                    }
                }

                this.DbContext.Borrowings.Add(borrowing);

                // move reserved → released on the balance
                var balance = entity.Equipment.EquipmentInventoryBalance;
                if (balance != null)
                {
                    balance.ReservedQuantity = Math.Max(0, balance.ReservedQuantity - form.ReleasedQuantity);
                    balance.ReleasedQuantity += form.ReleasedQuantity;
                }

                this.DbContext.InventoryMovements.Add(new InventoryMovement
                {
                    EquipmentId = entity.EquipmentId,
                    MovementType = "RELEASE",
                    FromState = "RESERVED",
                    ToState = "RELEASED",
                    Quantity = form.ReleasedQuantity,
                    ReferenceNumber = txn,
                    PerformedByUserId = borrowing.ReleasedByUserId,
                });

                // Fully released → COMPLETED; otherwise the rest stays held for a later release.
                entity.HeldQuantity = Math.Max(0, maxQty - form.ReleasedQuantity);
                var nextCode = entity.HeldQuantity == 0 ? "COMPLETED" : "PARTIALLY_RELEASED";
                var nextStatus = this.DbContext.ReservationStatuses.FirstOrDefault(s => s.Code == nextCode);
                if (nextStatus != null)
                {
                    entity.StatusId = nextStatus.StatusId;
                }
                if (entity.HeldQuantity == 0)
                {
                    entity.CompletedAtUtc = DateTime.UtcNow;
                }

                this._unitOfWork.SaveChanges();

                this.WriteNotification(entity.BorrowerUserId, "Equipment released",
                    $"Your reservation {entity.ReservationNumber} has been released. Transaction {txn}: {form.ReleasedQuantity} unit(s) of {entity.Equipment.EquipmentName} are now with you. Due back by {entity.ExpectedReturnAtUtc:dd MMM yyyy}.");

                this.WriteAudit("RELEASE", "Borrowing", borrowing.BorrowingId, txn,
                    $"Released {form.ReleasedQuantity} unit(s) of {entity.Equipment.AssetCode} under {txn} for reservation {entity.ReservationNumber}." +
                    (string.IsNullOrWhiteSpace(form.ReleaseNotes) ? string.Empty : " Notes: " + form.ReleaseNotes.Trim()));

                this.NotifyIfLowStock(entity.Equipment);

                return (true, null, borrowing.BorrowingId);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to release reservation {ReservationId}.", form?.ReservationId);
                return (false, ErrorMessages.ServerError, 0);
            }
        }

        public (bool Success, string Error) Cancel(long reservationId, string reason)
        {
            reason = reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return (false, ErrorMessages.CustodianRejectReasonRequired);
            }

            try
            {
                var entity = this._repository.GetReservationWithDetails(reservationId);
                if (entity == null)
                {
                    return (false, ErrorMessages.CustodianReservationNotFound);
                }
                if (!this._currentUser.CanActOn(entity.AssignedCustodianUserId))
                {
                    return (false, ErrorMessages.CustodianNotAssigned);
                }

                var code = entity.Status.Code;
                if (code != "PENDING" && code != "APPROVED" && code != "READY_FOR_RELEASE" && code != "PARTIALLY_RELEASED")
                {
                    return (false, ErrorMessages.CustodianReservationNotPending);
                }

                // A partially released reservation keeps its borrowing; only the unreleased rest is cancelled.
                var targetCode = code == "PARTIALLY_RELEASED" ? "COMPLETED" : "CANCELLED";
                var target = this.DbContext.ReservationStatuses.FirstOrDefault(s => s.Code == targetCode);
                if (target == null)
                {
                    return (false, ErrorMessages.ServerError);
                }

                var released = ReleaseHold(entity, this.RemainingHold(entity));
                entity.HeldQuantity = 0;
                entity.StatusId = target.StatusId;
                entity.CompletedAtUtc = DateTime.UtcNow;
                this.ClaimIfPooled(entity);
                this._unitOfWork.SaveChanges();

                this.WriteNotification(entity.BorrowerUserId, "Reservation cancelled",
                    $"Your reservation {entity.ReservationNumber} for {entity.Equipment.EquipmentName} was cancelled by the custodian. Reason: {reason}");

                this.WriteAudit("CANCEL", "Reservation", entity.ReservationId, entity.ReservationNumber,
                    $"Cancelled reservation {entity.ReservationNumber} ({released} held unit(s) released). Reason: {reason}");

                return (true, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                return (false, ErrorMessages.CustodianReservationNotPending);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to cancel reservation {ReservationId}.", reservationId);
                return (false, ErrorMessages.ServerError);
            }
        }

        public ScanResultViewModel ResolveScan(string code)
        {
            var result = new ScanResultViewModel { Code = code?.Trim(), Searched = !string.IsNullOrWhiteSpace(code) };
            if (!result.Searched)
            {
                return result;
            }

            var term = result.Code.ToUpperInvariant();
            var scope = this._currentUser.GetCustodianScope();

            // 1) Reservation number (printed on the pickup slip).
            var reservation = this.DbContext.Reservations.AsNoTracking()
                .Where(r => r.ReservationNumber.ToUpper() == term)
                .Where(r => scope == null || r.AssignedCustodianUserId == null || r.AssignedCustodianUserId == scope)
                .Select(r => new { r.ReservationId, r.ReservationNumber, Status = r.Status.Code, StatusText = r.Status.Description, r.Equipment.EquipmentName, Borrower = r.BorrowerUser.FirstName + " " + r.BorrowerUser.LastName })
                .FirstOrDefault();
            if (reservation != null)
            {
                result.EquipmentName = reservation.EquipmentName;
                result.Matches.Add(new ScanMatchViewModel
                {
                    Kind = reservation.Status == "APPROVED" || reservation.Status == "PARTIALLY_RELEASED" ? "RELEASE" : "REVIEW",
                    Id = reservation.ReservationId,
                    Title = reservation.ReservationNumber,
                    Subtitle = $"{reservation.Borrower} · {reservation.StatusText}",
                });
                return result;
            }

            // 2) Equipment asset code (printed on the item label).
            var equipment = this.DbContext.Equipment.AsNoTracking()
                .Where(e => e.AssetCode.ToUpper() == term)
                .Select(e => new { e.EquipmentId, e.EquipmentName })
                .FirstOrDefault();
            if (equipment == null)
            {
                return result;
            }

            result.EquipmentName = equipment.EquipmentName;
            result.Matches.AddRange(this.DbContext.Reservations.AsNoTracking()
                .Where(r => r.EquipmentId == equipment.EquipmentId && (r.Status.Code == "APPROVED" || r.Status.Code == "PARTIALLY_RELEASED"))
                .Where(r => scope == null || r.AssignedCustodianUserId == null || r.AssignedCustodianUserId == scope)
                .OrderBy(r => r.RequestedStartAtUtc)
                .Select(r => new ScanMatchViewModel
                {
                    Kind = "RELEASE",
                    Id = r.ReservationId,
                    Title = "Release " + r.ReservationNumber,
                    Subtitle = r.BorrowerUser.FirstName + " " + r.BorrowerUser.LastName + " · " + r.ApprovedQuantity.ToString() + " unit(s)",
                })
                .ToList());
            result.Matches.AddRange(this.DbContext.Borrowings.AsNoTracking()
                .Where(b => b.EquipmentId == equipment.EquipmentId
                    && (b.Status.Code == "ACTIVE" || b.Status.Code == "PARTIALLY_RETURNED" || b.Status.Code == "OVERDUE"))
                .Where(b => scope == null || b.Reservation == null || b.Reservation.AssignedCustodianUserId == null || b.Reservation.AssignedCustodianUserId == scope)
                .OrderBy(b => b.DueAtUtc)
                .Select(b => new ScanMatchViewModel
                {
                    Kind = "RETURN",
                    Id = b.BorrowingId,
                    Title = "Return " + b.TransactionNumber,
                    Subtitle = b.BorrowerUser.FirstName + " " + b.BorrowerUser.LastName + " · " + b.Status.Description,
                })
                .ToList());

            return result;
        }

        /// <summary>
        /// Units still held on the balance for this reservation: the full request while PENDING,
        /// otherwise the approved quantity minus what has already been released.
        /// </summary>
        private int RemainingHold(Reservation entity)
        {
            if (entity.Status?.Code == "PENDING")
            {
                return entity.RequestedQuantity;
            }

            var approved = entity.ApprovedQuantity > 0 ? entity.ApprovedQuantity : entity.RequestedQuantity;
            return Math.Max(0, approved - this._repository.SumReleasedForReservation(entity.ReservationId));
        }

        /// <summary>Returns held units to the available pool. Returns how many were released.</summary>
        private static int ReleaseHold(Reservation entity, int quantity)
        {
            var balance = entity.Equipment?.EquipmentInventoryBalance;
            if (balance == null || quantity <= 0)
            {
                return 0;
            }

            var released = Math.Min(quantity, balance.ReservedQuantity);
            balance.ReservedQuantity -= released;
            return released;
        }

        public (bool Success, string Error) Claim(long reservationId)
        {
            try
            {
                var actorId = this._currentUser.GetActorUserId();
                var entity = this._repository.GetReservationWithDetails(reservationId);
                if (entity == null || actorId == null)
                {
                    return (false, ErrorMessages.CustodianReservationNotFound);
                }

                if (entity.AssignedCustodianUserId != null || (entity.Status.Code != "PENDING" && entity.Status.Code != "APPROVED"))
                {
                    return (false, ErrorMessages.CustodianClaimUnavailable);
                }

                entity.AssignedCustodianUserId = actorId;
                this._unitOfWork.SaveChanges();

                this.WriteAudit("CLAIM", "Reservation", entity.ReservationId, entity.ReservationNumber,
                    $"Claimed reservation {entity.ReservationNumber} from the shared pool.");

                return (true, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                return (false, ErrorMessages.CustodianClaimUnavailable);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to claim reservation {ReservationId}.", reservationId);
                return (false, ErrorMessages.ServerError);
            }
        }

        /// <summary>A custodian (not an admin) acting on a pooled reservation takes ownership of it.</summary>
        private void ClaimIfPooled(Reservation entity)
        {
            if (entity.AssignedCustodianUserId == null && !this._currentUser.IsActorAdmin())
            {
                entity.AssignedCustodianUserId = this._currentUser.GetActorUserId();
            }
        }

        public ActiveBorrowingsIndexViewModel GetActiveBorrowings(string search, int page, int pageSize)
        {
            var result = this._repository.GetActiveBorrowingsPaged(search, page, pageSize, this._currentUser.GetCustodianScope());
            return new ActiveBorrowingsIndexViewModel
            {
                Page = new PagedResult<ActiveBorrowingRowViewModel>
                {
                    Items = result.Items.Select(this.MapActiveBorrowing).ToList(),
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount,
                },
                Search = search,
            };
        }

        private CustodianReservationRowViewModel MapReservationRow(Reservation entity)
        {
            return new CustodianReservationRowViewModel
            {
                ReservationId = entity.ReservationId,
                ReservationNumber = entity.ReservationNumber,
                BorrowerName = entity.BorrowerUser.FirstName + " " + entity.BorrowerUser.LastName,
                EquipmentName = entity.Equipment.EquipmentName,
                AssetCode = entity.Equipment.AssetCode,
                RequestedQuantity = entity.RequestedQuantity,
                PickupLocationName = entity.PickupLocation?.LocationName,
                StatusCode = entity.Status.Code,
                StatusDescription = entity.Status.Description,
                SubmittedAtUtc = entity.SubmittedAtUtc,
                RequestedStartAtUtc = entity.RequestedStartAtUtc,
                ExpectedReturnAtUtc = entity.ExpectedReturnAtUtc,
                AssignedCustodianUserId = entity.AssignedCustodianUserId,
                AssignedCustodianName = entity.AssignedCustodianUser == null ? null : entity.AssignedCustodianUser.FirstName + " " + entity.AssignedCustodianUser.LastName,
            };
        }

        private ActiveBorrowingRowViewModel MapActiveBorrowing(Borrowing entity)
        {
            var now = DateTime.UtcNow;
            var overdue = entity.DueAtUtc < now ? (int)Math.Floor((now - entity.DueAtUtc).TotalDays) : 0;
            return new ActiveBorrowingRowViewModel
            {
                BorrowingId = entity.BorrowingId,
                TransactionNumber = entity.TransactionNumber,
                BorrowerName = entity.BorrowerUser.FirstName + " " + entity.BorrowerUser.LastName,
                EquipmentName = entity.Equipment.EquipmentName,
                AssetCode = entity.Equipment.AssetCode,
                ReleasedQuantity = entity.ReleasedQuantity,
                ReleasedAtUtc = entity.ReleasedAtUtc,
                DueAtUtc = entity.DueAtUtc,
                DaysOverdue = overdue,
                IsOverdue = overdue > 0,
                ReleasedBy = entity.ReleasedByUser != null ? entity.ReleasedByUser.FirstName + " " + entity.ReleasedByUser.LastName : null,
                StatusCode = entity.Status.Code,
                StatusDescription = entity.Status.Description,
                InspectionStatus = entity.InspectionStatus.Description,
            };
        }

        private void WriteNotification(long recipientUserId, string title, string message)
        {
            this._notifications.Notify(recipientUserId, NotificationTypes.Reservation, title, message);
        }

        /// <summary>
        /// Alerts custodians and admins when a release leaves the item at or below the policy's
        /// low-stock threshold.
        /// </summary>
        private void NotifyIfLowStock(Equipment equipment)
        {
            var balance = equipment?.EquipmentInventoryBalance;
            if (balance == null)
            {
                return;
            }

            var available = balance.TotalQuantity - balance.ReservedQuantity - balance.ReleasedQuantity
                - balance.AwaitingInspectionQuantity - balance.MaintenanceQuantity - balance.UnreconciledQuantity;
            var threshold = this.DbContext.BorrowingPolicyVersions
                .AsNoTracking()
                .Where(p => p.IsCurrent)
                .Select(p => (int?)p.LowStockThreshold)
                .FirstOrDefault() ?? 2;

            if (available > threshold)
            {
                return;
            }

            this._notifications.Notify(
                this._notifications.GetActiveUserIdsInRoles(RoleCodes.Technician, RoleCodes.Admin),
                NotificationTypes.Alert,
                $"Low stock: {equipment.EquipmentName}",
                $"{equipment.EquipmentName} ({equipment.AssetCode}) has {Math.Max(0, available)} unit(s) available, at or below the low-stock threshold of {threshold}.");
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
