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
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Data.QueryModels;
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
        private const string BorrowingStatusReturned = BorrowingStatusCodes.Returned;

        private readonly IUnitOfWork _unitOfWork;
        private readonly ILookupRepository _lookups;
        private readonly IAuditLogRepository _auditLogs;
        private readonly IReservationRepository _reservationRepository;
        private readonly ICurrentUserService _currentUser;
        private readonly INotificationDispatcher _notifications;
        private readonly ICustodianAssignmentService _assignments;
        private readonly ReservationLifecycleOptions _lifecycle;
        private readonly IMapper _mapper;

        private readonly IUserRepository _users;
        private readonly ICustodianAssignmentRepository _assignmentRepository;
        private readonly IInventoryRepository _inventory;
        private readonly ICustodianRepository _custodianRepository;
        private readonly ISettingsRepository _settings;
        private readonly IBorrowingRepository _borrowings;

        public ReservationService(
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            IReservationRepository reservationRepository,
            ICurrentUserService currentUser,
            INotificationDispatcher notifications,
            ICustodianAssignmentService assignments,
            Microsoft.Extensions.Options.IOptions<ReservationLifecycleOptions> lifecycle,
            IMapper mapper,
            IAuditLogRepository auditLogs,
            ILookupRepository lookups,
            IUserRepository users,
            ICustodianAssignmentRepository assignmentRepository,
            IInventoryRepository inventory,
            ICustodianRepository custodianRepository,
            ISettingsRepository settings,
            IBorrowingRepository borrowings)
            : base(loggerFactory)
        {
            this._users = users;
            this._assignmentRepository = assignmentRepository;
            this._inventory = inventory;
            this._custodianRepository = custodianRepository;
            this._settings = settings;
            this._borrowings = borrowings;
            this._lookups = lookups;
            this._auditLogs = auditLogs;
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
            var policy = this.GetActivePolicy();
            var summary = new ReservationEquipmentSummaryViewModel
            {
                EquipmentId = row.EquipmentId,
                EquipmentName = row.EquipmentName,
                AssetCode = row.AssetCode,
                CategoryName = row.CategoryName,
                LocationName = row.LocationName,
                ConditionName = row.ConditionName,
                TotalQuantity = row.TotalQuantity,
                AvailableUnits = row.AvailableUnits,
                MaxUnitsPerBorrower = policy.MaxUnitsPerBorrower,
                ReservationLeadTimeDays = policy.ReservationLeadTimeDays,
                MaxAdvanceBookingDays = policy.MaxAdvanceBookingDays,
                MaxBorrowDays = string.Equals(this._currentUser.GetActorRoleCode(), RoleCodes.Faculty, StringComparison.OrdinalIgnoreCase)
                    ? policy.FacultyMaxBorrowDays
                    : policy.MaxBorrowDays,
                BlockedReason = GetReservationBlock(entity),
            };

            return new ReservationFormContextViewModel
            {
                Form = new ReservationFormViewModel
                {
                    EquipmentId = entity.EquipmentId,
                    Quantity = summary.MaxSelectable >= 1 ? 1 : null,
                },
                Equipment = summary,
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
            // Blocked items (maintenance, broken, retired, damaged...) have nothing available to borrowers.
            var block = GetReservationBlock(entity);
            var available = block != null ? 0 : this.ComputeAvailableUnits(balance);
            var isMaintenance = string.Equals(
                entity.OperationalStatus?.Code, ConditionCodes.UnderMaintenance, StringComparison.OrdinalIgnoreCase);

            var reason = block ?? (available <= 0 ? "Equipment is not available for reservation." : null);

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

        /// <inheritdoc />
        public string GetReserveBlockReason(long? equipmentId = null)
        {
            var actorUserId = this._currentUser.GetActorUserId();
            if (!actorUserId.HasValue)
            {
                return ErrorMessages.ReservationOperatorNotSet;
            }

            var statusCode = this._users.GetAccountStatusCode(actorUserId.Value);
            switch (statusCode)
            {
                case AccountStatusCodes.Active:
                    break;
                case AccountStatusCodes.Unverified:
                    return "Your account is still pending verification. You can reserve equipment once an administrator approves it.";
                case AccountStatusCodes.Rejected:
                    return "Your account verification was rejected. Submit an appeal from your dashboard.";
                default:
                    return ErrorMessages.ReservationAccountNotActive;
            }

            if (equipmentId.HasValue)
            {
                return this._assignments.ResolveCustodian(actorUserId.Value, equipmentId.Value).HasValue
                    ? null
                    : ErrorMessages.ReservationNoCustodian;
            }

            return this._assignmentRepository.BorrowerHasAssignmentWithCustodianStatus(actorUserId.Value, AccountStatusCodes.Active)
                ? null
                : ErrorMessages.ReservationNoCustodian;
        }

        /// <summary>
        /// Returns why an item can't be reserved right now (status not Available, or a current condition flagged
        /// <c>AffectsAvailability</c>: damaged, missing parts, lost), or null when it can be.
        /// </summary>
        private static string GetReservationBlock(Equipment equipment)
        {
            var status = equipment.OperationalStatus?.Code;
            if (string.Equals(status, ConditionCodes.UnderMaintenance, StringComparison.OrdinalIgnoreCase))
            {
                return ErrorMessages.ReservationEquipmentUnderMaintenance;
            }

            // Broken, retired or any other non-Available status takes the item out of circulation.
            if (status != null && !string.Equals(status, ConditionCodes.Available, StringComparison.OrdinalIgnoreCase))
            {
                return ErrorMessages.ReservationEquipmentOutOfService;
            }

            if (equipment.CurrentCondition?.AffectsAvailability == true)
            {
                return ErrorMessages.ReservationEquipmentConditionBlocked;
            }

            return null;
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

                // Maintenance / damaged items can't be reserved, whatever the client sends.
                var block = GetReservationBlock(equipment);
                if (block != null)
                {
                    return (false, block, form.EquipmentId);
                }

                var policy = this.GetActivePolicy();
                if (!policy.PolicyVersionId.HasValue)
                {
                    return (false, ErrorMessages.ReservationPolicyMissing, form.EquipmentId);
                }

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

                // Pickup and return are booked in whole-hour slots (1:00 PM, 2:00 PM, ...).
                if (form.BorrowingTime.Value.Minute != 0 || form.ExpectedReturnTime.Value.Minute != 0)
                {
                    return (false, "Pick a borrowing and return time on the hour (for example 1:00 PM).", form.EquipmentId);
                }

                var now = DateTime.UtcNow;
                if (requestedStart < now)
                {
                    return (false, ErrorMessages.ReservationPastDate, form.EquipmentId);
                }

                if (expectedReturn <= requestedStart)
                {
                    return (false, ErrorMessages.ReservationReturnBeforeStart, form.EquipmentId);
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
                var borrower = this._users.GetByIdWithDetails(actorUserId.Value)
                    ?? throw new InvalidOperationException("Sequence contains no elements");

                // Signed-in accounts that are not ACTIVE (unverified, rejected, suspended...) cannot reserve.
                if (borrower.AccountStatus?.Code != AccountStatusCodes.Active)
                {
                    var message = borrower.AccountStatus?.Code switch
                    {
                        AccountStatusCodes.Unverified => "Your account is still pending verification.",
                        AccountStatusCodes.Rejected => "Your account verification was rejected. Submit an appeal from your dashboard.",
                        _ => ErrorMessages.ReservationAccountNotActive,
                    };
                    return (false, message, form.EquipmentId);
                }

                var isFaculty = string.Equals(borrower.Role?.RoleCode, RoleCodes.Faculty, StringComparison.OrdinalIgnoreCase);
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
                    statusIds[ReservationStatusCodes.Pending],
                    statusIds[ReservationStatusCodes.Approved],
                };
                var activeCount = this._reservationRepository.CountForBorrowerWithStatusIds(actorUserId.Value, activeStatusIds);
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
                var pickupExists = this._reservationRepository.IsActivePickupLocation(form.PickupLocationId);
                if (!pickupExists)
                {
                    return (false, ErrorMessages.ReservationPickupLocationInvalid, form.EquipmentId);
                }

                // Every reservation must have an owner before any stock is held.
                var custodianId = this._assignments.ResolveCustodian(actorUserId.Value, equipment.EquipmentId);
                if (!custodianId.HasValue)
                {
                    return (false, ErrorMessages.ReservationNoCustodian, form.EquipmentId);
                }

                var quantity = form.Quantity.Value;
                var submittedAt = DateTime.UtcNow;
                using var transaction = this._unitOfWork.BeginTransaction();

                // Atomic hold: only succeeds while enough units are still free, so two borrowers
                // racing for the last unit cannot both get it.
                var held = this._inventory.TryHold(equipment.EquipmentId, quantity);
                if (held == 0)
                {
                    return (false, ErrorMessages.ReservationStockTaken, form.EquipmentId);
                }

                var reservation = new Reservation
                {
                    ReservationNumber = this.GenerateReservationNumber(submittedAt),
                    BorrowerUserId = actorUserId.Value,
                    EquipmentId = equipment.EquipmentId,
                    PickupLocationId = form.PickupLocationId.Value,
                    RequestedQuantity = quantity,
                    ApprovedQuantity = 0,
                    HeldQuantity = quantity,
                    RequestedStartAtUtc = requestedStart,
                    ExpectedReturnAtUtc = expectedReturn,
                    Purpose = form.Purpose?.Trim(),
                    BorrowingPolicyVersionId = policy.PolicyVersionId.Value,
                    StatusId = statusIds[ReservationStatusCodes.Pending],
                    SubmittedAtUtc = submittedAt,
                    CompletedAtUtc = null,
                    AssignedCustodianUserId = custodianId,
                };

                this._reservationRepository.Add(reservation);

                // Two borrowers submitting at once can draw the same number; retry with a fresh one.
                // The stock hold above already happened atomically, so only the number is retried.
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        this._unitOfWork.SaveChanges();
                        break;
                    }
                    catch (DbUpdateException ex) when (attempt < 5 && IsUniqueIndexViolation(ex, "UQ_reservations_reservation_number"))
                    {
                        reservation.ReservationNumber = this.GenerateReservationNumber(DateTime.UtcNow);
                    }
                }

                var notesSuffix = string.IsNullOrWhiteSpace(form.AdditionalNotes)
                    ? string.Empty
                    : $" Notes: {form.AdditionalNotes.Trim()}";
                this.WriteAudit("CREATE", "Reservation", reservation.ReservationId, reservation.ReservationNumber,
                    $"Submitted reservation {reservation.ReservationNumber} for '{equipment.EquipmentName}' " +
                    $"({quantity} unit(s), start {requestedStart:yyyy-MM-dd HH:mm} UTC) as PENDING.{notesSuffix}");
                transaction.Commit();

                this.NotifyStaffOfSubmission(reservation, equipment.EquipmentName);

                return (true, null, reservation.ReservationId);
            }
            catch (DbUpdateConcurrencyException)
            {
                this._logger.LogWarning("Concurrency conflict while creating a reservation for equipment {EquipmentId}.", form?.EquipmentId);
                return (false, ErrorMessages.ReservationCreateConflict, form?.EquipmentId ?? 0);
            }
            catch (DbUpdateException ex)
            {
                // Typically two submissions racing for the same reservation number; nothing was committed.
                this._logger.LogWarning(ex, "Reservation insert rejected by the database for equipment {EquipmentId}.", form?.EquipmentId);
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
            var isOwner = operatorUserId.HasValue && entity.BorrowerUserId == operatorUserId.Value;

            // Borrowers see only their own reservations; custodians only those routed to them or
            // still in the shared pool (the same rule as the review screen); admins see all.
            if (!isOwner && !(this.IsStaffOperator() && this._currentUser.CanActOn(entity.AssignedCustodianUserId)))
            {
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

                var statusCode = this._lookups.GetReservationStatusCodeById(entity.StatusId);
                if (!this.IsOpenStatus(statusCode))
                {
                    return (false, ErrorMessages.ReservationNotCancellable);
                }

                // Give back exactly what this reservation still holds (request while PENDING,
                // approved-minus-released once APPROVED).
                var hold = ReservationRules.RemainingHold(entity, statusCode, this._custodianRepository.SumReleasedForReservation(entity.ReservationId));

                var cancelledStatusId = this.GetReservationStatusIds()[ReservationStatusCodes.Cancelled];
                entity.StatusId = cancelledStatusId;
                entity.StatusReason = "Cancelled by you.";
                entity.HeldQuantity = 0;
                entity.CompletedAtUtc = DateTime.UtcNow;

                var balance = this._inventory.GetTrackedBalance(entity.EquipmentId);
                if (balance != null)
                {
                    balance.ReservedQuantity = Math.Max(0, balance.ReservedQuantity - hold);
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
            return this._reservationRepository.GetActivePickupLocations()
                .Select(l => new LookupViewModel { Id = l.LocationId, Text = l.LocationName })
                .ToList();
        }

        public string GetActiveOperatorName()
        {
            return this._currentUser.GetActorName();
        }

        public PolicySettings GetActivePolicy()        {
            // Latest version wins (per plan: new policy row = new version, history is never edited).
            var latest = this._settings.GetLatestPolicyNoTracking();

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
                MaxBorrowDays = latest.MaxBorrowDurationDays,
                MaxUnitsPerBorrower = latest.MaxUnitsPerBorrower > 0 ? latest.MaxUnitsPerBorrower : 3,
                MaxActiveTransactions = latest.MaxActiveBorrowingUnits,
                ReservationLeadTimeDays = Math.Max(0, latest.ReservationLeadTimeDays),
                MaxAdvanceBookingDays = latest.MaxAdvanceBookingDays > 0 ? latest.MaxAdvanceBookingDays : 30,
                AutoCancelHours = this._lifecycle.AutoCancelHours,
                // Faculty never get a shorter limit than students.
                FacultyMaxBorrowDays = Math.Max(latest.FacultyMaxBorrowDays, latest.MaxBorrowDurationDays),
                BlockReservationWhenOverdue = latest.BlockReservationWhenOverdue,
            };
        }

        private static DateTime ToUtc(DateOnly? date, TimeOnly? time)
        {
            if (!date.HasValue || !time.HasValue)
            {
                return default;
            }

            // The form captures campus wall-clock time; views render with ToLocalTime(), so convert from local.
            return DateTime.SpecifyKind(date.Value.ToDateTime(time.Value), DateTimeKind.Local).ToUniversalTime();
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
            // Same overdue rule as every other screen: open borrowing, past due plus the grace period.
            var cutoff = BorrowingRules.GetOverdueCutoffUtc(this._settings, now);
            return this._borrowings.CountForBorrowerInStatusesDueBefore(borrowerUserId, BorrowingRules.OpenStatusCodes, cutoff) > 0;
        }

        private Dictionary<string, long> GetReservationStatusIds()
        {
            return this._lookups.GetReservationStatusIdsByCode();
        }

        /// <summary>
        /// Picks the next RSV-yyyy-#### sequence for the calendar year (constraint: UQ number).
        /// </summary>
        private string GenerateReservationNumber(DateTime submittedAt)
        {
            // Continue from the highest number issued this year. Order by length first so "-10000" ranks above
            // "-9999". Concurrent submits can still race; Create retries on the unique index.
            var prefix = $"RSV-{submittedAt.Year}-";
            var last = this._reservationRepository.GetLastNumberWithPrefix(prefix);

            var next = 1;
            if (last != null && int.TryParse(last.Substring(prefix.Length), out var n))
            {
                next = n + 1;
            }

            return $"{prefix}{next:D4}";
        }

        private bool IsOpenStatus(string statusCode)
        {
            return string.Equals(statusCode, ReservationStatusCodes.Pending, StringComparison.Ordinal)
                || string.Equals(statusCode, ReservationStatusCodes.Approved, StringComparison.Ordinal);
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

            var roleCode = this._users.GetRoleCode(operatorUserId.Value);

            return string.Equals(roleCode, RoleCodes.Admin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleCode, RoleCodes.Technician, StringComparison.OrdinalIgnoreCase);
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

            // An item that can't be reserved (maintenance, broken, retired, damaged...) has nothing
            // available to borrowers, whatever is on the shelf.
            row.UnavailableReason = GetReservationBlock(entity);
            if (row.UnavailableReason != null)
            {
                row.AvailableUnits = 0;
            }

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
                var borrowerName = this._users.GetFullName(reservation.BorrowerUserId);
                this._notifications.Notify(recipients, NotificationTypes.Reservation,
                    $"New reservation {reservation.ReservationNumber}",
                    $"{borrowerName} requested {reservation.RequestedQuantity} unit(s) of {equipmentName} starting {reservation.RequestedStartAtUtc:dd MMM yyyy}. Review it in the custodian console.",
                    $"/Custodian/Review/{reservation.ReservationId}");
            }
            catch (Exception ex)
            {
                // The reservation is already saved; a failed alert must not undo it.
                this._logger.LogWarning(ex, "Failed to notify staff about reservation {ReservationId}.", reservation.ReservationId);
            }
        }

        private void WriteAudit(string actionType, string entityType, long entityId, string referenceNumber, string details)
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
            this._unitOfWork.SaveChanges();
        }
    }
}
