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
using System.Collections.Generic;
using System.Linq;
using ErrorMessages = ASI.Basecode.Resources.Messages.Errors;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Borrower reservation flow service: policy validation, reservation create/cancel/read
    /// and the borrower equipment availability listing. Data access stays in the repository;
    /// this layer owns the business rules.
    /// </summary>
    public class ReservationService : ServiceBase, IReservationService
    {
        /// <summary>Inventory states counted as unavailable when computing free units (EquipmentService formula).</summary>
        private const string BorrowingStatusReturned = "RETURNED";

        private readonly IUnitOfWork _unitOfWork;
        private readonly IReservationRepository _reservationRepository;
        private readonly ICurrentUserService _currentUser;
        private readonly INotificationDispatcher _notifications;
        private readonly ICustodianAssignmentService _assignments;
        private readonly ReservationLifecycleOptions _lifecycle;
        private readonly IMapper _mapper;

        /// <summary>Direct EF access for codebook lookups and the small writes this service owns.</summary>
        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public ReservationService(
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            IReservationRepository reservationRepository,
            ICurrentUserService currentUser,
            INotificationDispatcher notifications,
            ICustodianAssignmentService assignments,
            Microsoft.Extensions.Options.IOptions<ReservationLifecycleOptions> lifecycle,
            IMapper mapper)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
            this._reservationRepository = reservationRepository;
            this._currentUser = currentUser;
            this._notifications = notifications;
            this._assignments = assignments;
            this._lifecycle = lifecycle?.Value ?? new ReservationLifecycleOptions();
            this._mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public EquipmentAvailabilityIndexViewModel GetAvailabilityViewModel(
            string search,
            long? categoryId,
            int page,
            int pageSize)
        {
            var result = this._reservationRepository.GetAvailableEquipmentPaged(search, categoryId, page, pageSize);

            return new EquipmentAvailabilityIndexViewModel
            {
                Page = new PagedResult<EquipmentAvailabilityRowViewModel>
                {
                    Items = result.Items.Select(this.MapToAvailabilityRow).ToList(),
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount,
                },
                Search = search,
                CategoryId = categoryId,
            };
        }

        public ReservationFormContextViewModel GetReservationFormViewModel(long equipmentId)
        {
            var entity = this._reservationRepository.GetReservableEquipment(equipmentId);
            if (entity == null)
            {
                return null;
            }

            var row = this.MapToAvailabilityRow(entity);
            return new ReservationFormContextViewModel
            {
                Form = new ReservationFormViewModel { EquipmentId = entity.EquipmentId },
                Equipment = new ReservationEquipmentSummaryViewModel
                {
                    EquipmentId = row.EquipmentId,
                    EquipmentName = row.EquipmentName,
                    AssetCode = row.AssetCode,
                    CategoryName = row.CategoryName,
                    LocationName = row.LocationName,
                    ConditionName = row.ConditionName,
                    TotalQuantity = row.TotalQuantity,
                    AvailableUnits = row.AvailableUnits,
                },
            };
        }

        public EquipmentDetailsViewModel GetEquipmentDetailsViewModel(long equipmentId)
        {
            var entity = this._reservationRepository.GetReservableEquipment(equipmentId);
            if (entity == null)
            {
                return null;
            }

            var row = this.MapToAvailabilityRow(entity);
            var policy = this.GetActivePolicy();
            var balance = entity.EquipmentInventoryBalance;
            var available = this.ComputeAvailableUnits(balance);
            var isMaintenance = string.Equals(
                entity.OperationalStatus?.Code, "UNDER_MAINTENANCE", StringComparison.OrdinalIgnoreCase);

            var reason = isMaintenance
                ? "Equipment is currently under maintenance."
                : (available <= 0 ? "Equipment is not available for reservation." : null);

            return new EquipmentDetailsViewModel
            {
                EquipmentId = row.EquipmentId,
                EquipmentName = row.EquipmentName,
                AssetCode = row.AssetCode,
                Description = entity.Description,
                CategoryName = row.CategoryName,
                LocationName = row.LocationName,
                ConditionName = row.ConditionName,
                OperationalStatusCode = entity.OperationalStatus?.Code,
                OperationalStatusName = entity.OperationalStatus?.Description,
                ImagePath = entity.ImagePath,
                TotalQuantity = balance?.TotalQuantity ?? 0,
                ReservedUnits = balance?.ReservedQuantity ?? 0,
                AvailableUnits = available,
                IsUnderMaintenance = isMaintenance,
                CanReserve = reason == null,
                UnavailableReason = reason,
                MaxBorrowDays = policy.MaxBorrowDays,
                FacultyMaxBorrowDays = policy.FacultyMaxBorrowDays,
                MaxUnitsPerBorrower = policy.MaxUnitsPerBorrower,
                ReservationLeadTimeDays = policy.ReservationLeadTimeDays,
            };
        }

        public (bool Success, string Error, long ReservationId) Create(ReservationFormViewModel form)
        {
            try
            {
                if (form == null || form.EquipmentId <= 0)
                {
                    return (false, ErrorMessages.ReservationEquipmentNotFound, 0);
                }

                var actorUserId = this._currentUser.GetActorUserId();
                if (!actorUserId.HasValue)
                {
                    return (false, ErrorMessages.ReservationOperatorNotSet, 0);
                }

                var equipment = this._reservationRepository.GetReservableEquipment(form.EquipmentId);
                if (equipment == null)
                {
                    return (false, ErrorMessages.ReservationEquipmentNotFound, form.EquipmentId);
                }

                var policy = this.GetActivePolicy();

                // Rule a) quantity bounds.
                if (form.Quantity is null || form.Quantity < 1)
                {
                    return (false, ErrorMessages.ReservationQuantityInvalid, form.EquipmentId);
                }

                if (form.Quantity > policy.MaxUnitsPerBorrower)
                {
                    return (false, ErrorMessages.ReservationQuantityAboveMax, form.EquipmentId);
                }

                var balance = equipment.EquipmentInventoryBalance;
                var available = this.ComputeAvailableUnits(balance);
                if (form.Quantity > available)
                {
                    return (false, ErrorMessages.ReservationQuantityAboveAvailable, form.EquipmentId);
                }

                // Rule b) schedule bounds (local input treated as UTC per the dev convention).
                var requestedStart = ToUtc(form.BorrowingDate, form.BorrowingTime);
                var expectedReturn = ToUtc(form.ExpectedReturnDate, form.ExpectedReturnTime);
                if (requestedStart == default || expectedReturn == default)
                {
                    return (false, ErrorMessages.ReservationScheduleMissing, form.EquipmentId);
                }

                var now = DateTime.UtcNow;
                if (requestedStart < now)
                {
                    return (false, ErrorMessages.ReservationPastDate, form.EquipmentId);
                }

                if (requestedStart < now.AddDays(policy.ReservationLeadTimeDays))
                {
                    return (false, ErrorMessages.ReservationLeadTimeTooShort, form.EquipmentId);
                }

                if (requestedStart > now.AddDays(policy.MaxAdvanceBookingDays))
                {
                    return (false, ErrorMessages.ReservationTooFarAhead, form.EquipmentId);
                }

                // Rule c) duration limit with the faculty override.
                var borrower = this.DbContext.Users
                    .AsNoTracking()
                    .Include(u => u.Role)
                    .Include(u => u.AccountStatus)
                    .First(u => u.UserId == actorUserId.Value);

                // Unverified accounts may sign in but cannot submit a reservation ([1.1] special scenario).
                if (string.Equals(borrower.AccountStatus?.Code, "UNVERIFIED", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "Your account is still pending verification.", form.EquipmentId);
                }

                if (string.Equals(borrower.AccountStatus?.Code, "REJECTED", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, "Your account verification was rejected. Submit an appeal from your dashboard.", form.EquipmentId);
                }

                var isFaculty = string.Equals(borrower.Role?.RoleCode, "FACULTY", StringComparison.OrdinalIgnoreCase);
                var maxBorrowDays = isFaculty ? policy.FacultyMaxBorrowDays : policy.MaxBorrowDays;
                var durationDays = (expectedReturn - requestedStart).TotalDays;
                if (durationDays > maxBorrowDays)
                {
                    return (false, ErrorMessages.ReservationDurationTooLong, form.EquipmentId);
                }

                // Rule d) concurrent open request limit.
                var statusIds = this.GetReservationStatusIds();
                var activeStatusIds = new List<long>
                {
                    statusIds["PENDING"],
                    statusIds["APPROVED"],
                };
                var activeCount = this.DbContext.Reservations
                    .Count(r => r.BorrowerUserId == actorUserId.Value
                        && activeStatusIds.Contains(r.StatusId));
                if (activeCount >= policy.MaxActiveTransactions)
                {
                    return (false, ErrorMessages.ReservationActiveLimitReached, form.EquipmentId);
                }

                // Rule e) overdue gate.
                if (policy.BlockReservationWhenOverdue && this.HasOverdueBorrowing(actorUserId.Value, now))
                {
                    return (false, ErrorMessages.ReservationOverdueBlock, form.EquipmentId);
                }

                // Acknowledgements — both required (server-side check per the Figma contract).
                if (!form.AcknowledgeRules || !form.AcknowledgeResponsibility)
                {
                    return (false, ErrorMessages.ReservationMissingAck, form.EquipmentId);
                }

                // Pickup point must reference an active pickup location.
                var pickupExists = this.DbContext.Locations
                    .Any(l => l.LocationId == form.PickupLocationId
                        && l.IsPickupLocation
                        && l.IsActive);
                if (!pickupExists)
                {
                    return (false, ErrorMessages.ReservationPickupLocationInvalid, form.EquipmentId);
                }

                // Persist: hold the requested units, insert the PENDING reservation, audit.
                var pendingStatusId = statusIds["PENDING"];
                var submittedAt = DateTime.UtcNow;
                var reservation = new Reservation
                {
                    ReservationNumber = this.GenerateReservationNumber(submittedAt),
                    BorrowerUserId = actorUserId.Value,
                    EquipmentId = equipment.EquipmentId,
                    PickupLocationId = form.PickupLocationId.Value,
                    RequestedQuantity = form.Quantity.Value,
                    ApprovedQuantity = 0,
                    HeldQuantity = form.Quantity.Value, // held on the balance below
                    RequestedStartAtUtc = requestedStart,
                    ExpectedReturnAtUtc = expectedReturn,
                    Purpose = form.Purpose?.Trim(),
                    BorrowingPolicyVersionId = policy.PolicyVersionId.Value,
                    StatusId = pendingStatusId,
                    SubmittedAtUtc = submittedAt,
                    CompletedAtUtc = null,
                    AssignedCustodianUserId = this._assignments.ResolveCustodian(actorUserId.Value, equipment.EquipmentId),
                };

                this.DbContext.Reservations.Add(reservation);

                // Hold the units on the balance row (tracked) so no one else can promise them.
                var balanceToUpdate = this.DbContext.EquipmentInventoryBalances
                    .First(b => b.EquipmentId == equipment.EquipmentId);
                balanceToUpdate.ReservedQuantity += form.Quantity.Value;

                this._unitOfWork.SaveChanges();

                var notesSuffix = string.IsNullOrWhiteSpace(form.AdditionalNotes)
                    ? string.Empty
                    : $" Notes: {form.AdditionalNotes.Trim()}";
                this.WriteAudit("CREATE", "Reservation", reservation.ReservationId, reservation.ReservationNumber,
                    $"Submitted reservation {reservation.ReservationNumber} for '{equipment.EquipmentName}' " +
                    $"({form.Quantity} unit(s), start {requestedStart:yyyy-MM-dd HH:mm} UTC) as PENDING.{notesSuffix}");

                this.NotifyStaffOfSubmission(reservation, equipment.EquipmentName);

                return (true, null, reservation.ReservationId);
            }
            catch (DbUpdateConcurrencyException)
            {
                this._logger.LogWarning("Concurrency conflict while creating a reservation for equipment {EquipmentId}.", form?.EquipmentId);
                return (false, ErrorMessages.ReservationCreateConflict, form?.EquipmentId ?? 0);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to create a reservation for equipment {EquipmentId}.", form?.EquipmentId);
                return (false, ErrorMessages.ServerError, form?.EquipmentId ?? 0);
            }
        }

        public ReservationMineViewModel GetMineViewModel()
        {
            var operatorUserId = this._currentUser.GetActorUserId();
            var model = new ReservationMineViewModel
            {
                OperatorName = this._currentUser.GetActorName(),
            };
            if (!operatorUserId.HasValue)
            {
                return model;
            }

            model.Reservations = this._reservationRepository
                .GetForBorrower(operatorUserId.Value)
                .Select(entity =>
                {
                    var row = this._mapper.Map<ReservationListViewModel>(entity);
                    row.CanCancel = this.IsCanceller(entity.BorrowerUserId, operatorUserId) && this.IsOpenStatus(entity.Status?.Code);
                    return row;
                })
                .ToList();
            return model;
        }

        public ReservationDetailsViewModel GetDetailsViewModel(long reservationId)
        {
            var entity = this._reservationRepository.GetByIdWithDetails(reservationId);
            if (entity == null)
            {
                return null;
            }

            var operatorUserId = this._currentUser.GetActorUserId();
            var isStaff = this.IsStaffOperator();
            if (!isStaff && (!operatorUserId.HasValue || entity.BorrowerUserId != operatorUserId.Value))
            {
                // Borrowers may only view their own reservations.
                return null;
            }

            var model = this._mapper.Map<ReservationDetailsViewModel>(entity);
            model.CanCancel = this.IsCanceller(entity.BorrowerUserId, operatorUserId)
                && this.IsOpenStatus(entity.Status?.Code);
            return model;
        }

        public (bool Success, string Error) Cancel(long reservationId)
        {
            try
            {
                var operatorUserId = this._currentUser.GetActorUserId();
                if (!operatorUserId.HasValue)
                {
                    return (false, ErrorMessages.ReservationOperatorNotSet);
                }

                var entity = this._reservationRepository.GetTrackedById(reservationId);
                if (entity == null)
                {
                    return (false, ErrorMessages.ReservationNotFound);
                }

                if (entity.BorrowerUserId != operatorUserId.Value)
                {
                    // Owners only — staff cancellation arrives with the custodian portal (Phase 4).
                    return (false, ErrorMessages.ReservationNotFound);
                }

                var statusCode = this.DbContext.ReservationStatuses
                    .AsNoTracking()
                    .Where(s => s.StatusId == entity.StatusId)
                    .Select(s => s.Code)
                    .FirstOrDefault();
                if (!this.IsOpenStatus(statusCode))
                {
                    return (false, ErrorMessages.ReservationNotCancellable);
                }

                var cancelledStatusId = this.GetReservationStatusIds()["CANCELLED"];
                entity.StatusId = cancelledStatusId;
                entity.CompletedAtUtc = DateTime.UtcNow;

                // Give the units back for both cancellable stages: PENDING requests held them
                // at submission and APPROVED ones held them for pickup. Without this the
                // balance would leak reserved units for every cancelled request.
                var balance = this.DbContext.EquipmentInventoryBalances
                    .FirstOrDefault(b => b.EquipmentId == entity.EquipmentId);
                if (balance != null)
                {
                    balance.ReservedQuantity = Math.Max(0, balance.ReservedQuantity - entity.RequestedQuantity);
                }

                this._unitOfWork.SaveChanges();

                this.WriteAudit("CANCEL", "Reservation", entity.ReservationId, entity.ReservationNumber,
                    $"Cancelled reservation {entity.ReservationNumber} by the borrower while {statusCode}.");

                return (true, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                this._logger.LogWarning("Concurrency conflict while cancelling reservation {ReservationId}.", reservationId);
                return (false, ErrorMessages.ReservationOperationConflict);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to cancel reservation {ReservationId}.", reservationId);
                return (false, ErrorMessages.ServerError);
            }
        }

        public List<LookupViewModel> GetPickupLocationOptions()
        {
            return this.DbContext.Locations
                .AsNoTracking()
                .Where(l => l.IsPickupLocation && l.IsActive)
                .OrderBy(l => l.LocationName)
                .Select(l => new LookupViewModel { Id = l.LocationId, Text = l.LocationName })
                .ToList();
        }

        public string GetActiveOperatorName()
        {
            return this._currentUser.GetActorName();
        }

        public PolicySettings GetActivePolicy()        {
            // Latest version wins (per plan: new policy row = new version, history is never edited).
            var latest = this.DbContext.BorrowingPolicyVersions
                .AsNoTracking()
                .OrderByDescending(p => p.IsCurrent)
                .ThenByDescending(p => p.PolicyVersionId)
                .ThenByDescending(p => p.EffectiveFromUtc)
                .FirstOrDefault();

            // Defaults from the implementation plan — used verbatim when no policy row exists yet.
            if (latest == null)
            {
                return new PolicySettings
                {
                    PolicyVersionId = null,
                    VersionNumber = "DEFAULT",
                    MaxBorrowDays = 7,
                    MaxUnitsPerBorrower = 3,
                    MaxActiveTransactions = 3,
                    ReservationLeadTimeDays = 1,
                    MaxAdvanceBookingDays = 30,
                    AutoCancelHours = this._lifecycle.AutoCancelHours,
                    FacultyMaxBorrowDays = 21,
                    BlockReservationWhenOverdue = true,
                };
            }

            return new PolicySettings
            {
                PolicyVersionId = latest.PolicyVersionId,
                VersionNumber = latest.VersionNumber,
                // borrowing_policy_versions columns map onto the settings as follows; the rest are constants
                // until Phase 7 extends the table (see notes on the entity's actual property names).
                MaxBorrowDays = latest.MaxBorrowDurationDays,
                MaxUnitsPerBorrower = 3,
                MaxActiveTransactions = latest.MaxActiveBorrowingUnits,
                ReservationLeadTimeDays = 1,
                MaxAdvanceBookingDays = 30,
                AutoCancelHours = this._lifecycle.AutoCancelHours,
                FacultyMaxBorrowDays = 21,
                BlockReservationWhenOverdue = latest.BlockReservationWhenOverdue,
            };
        }

        private static DateTime ToUtc(DateOnly? date, TimeOnly? time)
        {
            if (!date.HasValue || !time.HasValue)
            {
                return default;
            }

            // Dev convention: the form captures wall-clock values and we store them as-is in UTC.
            return DateTime.SpecifyKind(date.Value.ToDateTime(time.Value), DateTimeKind.Utc);
        }

        private int ComputeAvailableUnits(EquipmentInventoryBalance balance)
        {
            if (balance == null)
            {
                return 0;
            }

            // Same formula as EquipmentService.MapToListItem.
            return balance.TotalQuantity
                - balance.ReservedQuantity
                - balance.ReleasedQuantity
                - balance.AwaitingInspectionQuantity
                - balance.MaintenanceQuantity
                - balance.UnreconciledQuantity;
        }

        private bool HasOverdueBorrowing(long borrowerUserId, DateTime now)
        {
            // An open overdue item blocks new requests: every borrowing state counts except a
            // clean RETURNED close (ACTIVE, PARTIALLY_RETURNED, OVERDUE, LOST).
            var openStatusIds = this.DbContext.BorrowingStatuses
                .AsNoTracking()
                .Where(s => s.Code != BorrowingStatusReturned)
                .Select(s => s.StatusId)
                .ToList();

            return this.DbContext.Borrowings
                .Any(b => b.BorrowerUserId == borrowerUserId
                    && b.DueAtUtc < now
                    && openStatusIds.Contains(b.StatusId));
        }

        private Dictionary<string, long> GetReservationStatusIds()
        {
            return this.DbContext.ReservationStatuses
                .AsNoTracking()
                .ToDictionary(s => s.Code, s => s.StatusId);
        }

        /// <summary>
        /// Picks the next RSV-yyyy-#### sequence for the calendar year (constraint: UQ number).
        /// </summary>
        private string GenerateReservationNumber(DateTime submittedAt)
        {
            var year = submittedAt.Year;
            var generatedThisYear = this.DbContext.Reservations.Count(r => r.SubmittedAtUtc.Year == year) + 1;

            // Bump past collisions (e.g. cancelled-sequence gaps from manual data fixes).
            string number;
            do
            {
                number = $"RSV-{year}-{generatedThisYear:D4}";
                generatedThisYear++;
            }
            while (this.DbContext.Reservations.Any(r => r.ReservationNumber == number));

            return number;
        }

        private bool IsOpenStatus(string statusCode)
        {
            return string.Equals(statusCode, "PENDING", StringComparison.Ordinal)
                || string.Equals(statusCode, "APPROVED", StringComparison.Ordinal);
        }

        private bool IsCanceller(long borrowerUserId, long? operatorUserId)
        {
            return operatorUserId.HasValue && borrowerUserId == operatorUserId.Value;
        }

        private bool IsStaffOperator()
        {
            var operatorUserId = this._currentUser.GetActorUserId();
            if (!operatorUserId.HasValue)
            {
                return false;
            }

            var roleCode = this.DbContext.Users
                .AsNoTracking()
                .Where(u => u.UserId == operatorUserId.Value)
                .Select(u => u.Role.RoleCode)
                .FirstOrDefault();

            return string.Equals(roleCode, "ADMIN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleCode, "TECHNICIAN", StringComparison.OrdinalIgnoreCase);
        }

        private EquipmentAvailabilityRowViewModel MapToAvailabilityRow(Equipment entity)
        {
            var row = this._mapper.Map<EquipmentAvailabilityRowViewModel>(entity);
            row.TotalQuantity = entity.EquipmentInventoryBalance?.TotalQuantity ?? 0;
            row.AvailableUnits = row.TotalQuantity
                - (entity.EquipmentInventoryBalance?.ReservedQuantity ?? 0)
                - (entity.EquipmentInventoryBalance?.ReleasedQuantity ?? 0)
                - (entity.EquipmentInventoryBalance?.AwaitingInspectionQuantity ?? 0)
                - (entity.EquipmentInventoryBalance?.MaintenanceQuantity ?? 0)
                - (entity.EquipmentInventoryBalance?.UnreconciledQuantity ?? 0);
            return row;
        }

        /// <summary>
        /// Tells staff a new request is waiting: the routed custodian when one is assigned,
        /// otherwise every custodian (shared pool), plus administrators.
        /// </summary>
        private void NotifyStaffOfSubmission(Reservation reservation, string equipmentName)
        {
            try
            {
                var recipients = reservation.AssignedCustodianUserId.HasValue
                    ? new List<long> { reservation.AssignedCustodianUserId.Value }
                    : this._notifications.GetActiveUserIdsInRoles(RoleCodes.Technician);
                recipients.AddRange(this._notifications.GetActiveUserIdsInRoles(RoleCodes.Admin));
                var borrowerName = this.DbContext.Users
                    .AsNoTracking()
                    .Where(u => u.UserId == reservation.BorrowerUserId)
                    .Select(u => u.FirstName + " " + u.LastName)
                    .FirstOrDefault();
                this._notifications.Notify(recipients, NotificationTypes.Reservation,
                    $"New reservation {reservation.ReservationNumber}",
                    $"{borrowerName} requested {reservation.RequestedQuantity} unit(s) of {equipmentName} starting {reservation.RequestedStartAtUtc:dd MMM yyyy}. Review it in the custodian console.");
            }
            catch (Exception ex)
            {
                // The reservation is already saved; a failed alert must not undo it.
                this._logger.LogWarning(ex, "Failed to notify staff about reservation {ReservationId}.", reservation.ReservationId);
            }
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
