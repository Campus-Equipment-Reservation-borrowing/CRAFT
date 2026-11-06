using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ErrorMessages = ASI.Basecode.Resources.Messages.Errors;

namespace ASI.Basecode.Services.Services
{
    public class ReturnProcessingService : ServiceBase, IReturnProcessingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IReturnProcessingRepository _repository;
        private readonly ICurrentUserService _currentUser;
        private readonly INotificationDispatcher _notifications;
        private readonly IMapper _mapper;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public ReturnProcessingService(
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            IReturnProcessingRepository repository,
            ICurrentUserService currentUser,
            INotificationDispatcher notifications,
            IMapper mapper)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
            this._repository = repository;
            this._currentUser = currentUser;
            this._notifications = notifications;
            this._mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public ReturnsIndexViewModel GetReturnsIndex(string search, int page, int pageSize)
        {
            var result = this._repository.GetReturnsPaged(search, page, pageSize, this._currentUser.GetCustodianScope());
            return new ReturnsIndexViewModel
            {
                Page = new PagedResult<ReturnRowViewModel>
                {
                    Items = result.Items.Select(r => new ReturnRowViewModel
                    {
                        ReturnId = r.ReturnId,
                        TransactionNumber = r.Borrowing.TransactionNumber,
                        BorrowerName = r.Borrowing.BorrowerUser.FirstName + " " + r.Borrowing.BorrowerUser.LastName,
                        EquipmentName = r.Borrowing.Equipment.EquipmentName,
                        AssetCode = r.Borrowing.Equipment.AssetCode,
                        ReturnedQuantity = r.ReturnedQuantity,
                        ReturnedAtUtc = r.ReturnedAtUtc,
                        WasReturnedLate = r.WasReturnedLate,
                        DaysOverdue = r.DaysOverdue,
                        ReceivedBy = r.ReceivedByUser != null ? r.ReceivedByUser.FirstName + " " + r.ReceivedByUser.LastName : null,
                    }).ToList(),
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount,
                },
                Search = search,
            };
        }

        public ReturnFormContextViewModel GetReturnFormContext(long borrowingId)
        {
            var borrowing = this._repository.GetBorrowingForReturn(borrowingId);
            if (borrowing == null || !this._currentUser.CanActOn(borrowing.Reservation?.AssignedCustodianUserId))
            {
                return null;
            }

            var alreadyReturned = borrowing.BorrowingReturns.Sum(r => r.ReturnedQuantity);
            var now = DateTime.UtcNow;
            var overdue = borrowing.DueAtUtc < now;
            var daysOverdue = overdue ? (int)Math.Floor((now - borrowing.DueAtUtc).TotalDays) : 0;

            return new ReturnFormContextViewModel
            {
                Form = new ReturnFormViewModel
                {
                    BorrowingId = borrowing.BorrowingId,
                    ReturnedQuantity = borrowing.ReleasedQuantity - alreadyReturned,
                    ReturnedAtUtc = now,
                    ReceivedByUserId = this._currentUser.GetActorUserId() ?? 0,
                },
                TransactionNumber = borrowing.TransactionNumber,
                BorrowerName = borrowing.BorrowerUser.FirstName + " " + borrowing.BorrowerUser.LastName,
                EquipmentName = borrowing.Equipment.EquipmentName,
                AssetCode = borrowing.Equipment.AssetCode,
                ReleasedQuantity = borrowing.ReleasedQuantity,
                AlreadyReturned = alreadyReturned,
                DueAtUtc = borrowing.DueAtUtc,
                IsOverdue = overdue,
                DaysOverdue = daysOverdue,
            };
        }

        public List<LookupViewModel> GetConditionOptions()
        {
            return this.DbContext.Conditions.AsNoTracking()
                .OrderBy(c => c.Code).Select(c => new LookupViewModel { Id = c.ConditionId, Text = c.Description }).ToList();
        }

        public List<LookupViewModel> GetInspectionStatusOptions()
        {
            return this.DbContext.InspectionStatuses.AsNoTracking()
                .OrderBy(s => s.StatusId).Select(s => new LookupViewModel { Id = s.StatusId, Text = s.Description }).ToList();
        }

        public (bool Success, string Error, long ReturnId) CreateReturn(ReturnFormViewModel form)
        {
            if (form == null || form.ReturnedQuantity < 1)
            {
                return (false, ErrorMessages.ServerError, 0);
            }

            try
            {
                var borrowing = this._repository.GetBorrowingForReturn(form.BorrowingId);
                if (borrowing == null)
                {
                    return (false, ErrorMessages.ServerError, 0);
                }

                if (!this._currentUser.CanActOn(borrowing.Reservation?.AssignedCustodianUserId))
                {
                    return (false, ErrorMessages.CustodianNotAssigned, 0);
                }

                var alreadyReturned = borrowing.BorrowingReturns.Sum(r => r.ReturnedQuantity);
                var remaining = borrowing.ReleasedQuantity - alreadyReturned;
                if (form.ReturnedQuantity > remaining)
                {
                    return (false, ErrorMessages.ServerError, 0);
                }

                var now = form.ReturnedAtUtc == default ? DateTime.UtcNow : DateTime.SpecifyKind(form.ReturnedAtUtc, DateTimeKind.Utc);
                var wasLate = now > borrowing.DueAtUtc;
                var daysOverdue = wasLate ? (int)Math.Floor((now - borrowing.DueAtUtc).TotalDays) : 0;

                var returnEntity = new BorrowingReturn
                {
                    BorrowingId = borrowing.BorrowingId,
                    ReturnedQuantity = form.ReturnedQuantity,
                    ReturnedAtUtc = now,
                    ReceivedByUserId = form.ReceivedByUserId > 0 ? form.ReceivedByUserId : (this._currentUser.GetActorUserId() ?? borrowing.ReleasedByUserId),
                    WasReturnedLate = wasLate,
                    DaysOverdue = daysOverdue,
                };

                this.DbContext.BorrowingReturns.Add(returnEntity);

                // update balance: released -> awaiting inspection (finalized by CreateInspection)
                var balance = borrowing.Equipment.EquipmentInventoryBalance;
                if (balance != null)
                {
                    balance.ReleasedQuantity = Math.Max(0, balance.ReleasedQuantity - form.ReturnedQuantity);
                    balance.AwaitingInspectionQuantity += form.ReturnedQuantity;
                }

                // update borrowing status
                var totalReturned = alreadyReturned + form.ReturnedQuantity;
                if (totalReturned >= borrowing.ReleasedQuantity)
                {
                    var returnedStatus = this.DbContext.BorrowingStatuses.FirstOrDefault(s => s.Code == "RETURNED");
                    if (returnedStatus != null)
                    {
                        borrowing.StatusId = returnedStatus.StatusId;
                    }
                }
                else
                {
                    var partialStatus = this.DbContext.BorrowingStatuses.FirstOrDefault(s => s.Code == "PARTIALLY_RETURNED");
                    if (partialStatus != null)
                    {
                        borrowing.StatusId = partialStatus.StatusId;
                    }
                }

                this.DbContext.InventoryMovements.Add(new InventoryMovement
                {
                    EquipmentId = borrowing.EquipmentId,
                    MovementType = "RETURN",
                    FromState = "RELEASED",
                    ToState = "AWAITING_INSPECTION",
                    Quantity = form.ReturnedQuantity,
                    ReferenceNumber = borrowing.TransactionNumber,
                    PerformedByUserId = returnEntity.ReceivedByUserId,
                });

                this._unitOfWork.SaveChanges();

                this.WriteNotification(borrowing.BorrowerUserId, "Equipment returned",
                    $"Return of {form.ReturnedQuantity} unit(s) of {borrowing.Equipment.EquipmentName} recorded under {borrowing.TransactionNumber}." +
                    (wasLate ? $" Returned {daysOverdue} day(s) late." : " Returned on time."));

                this.WriteAudit("RETURN", "Borrowing", borrowing.BorrowingId, borrowing.TransactionNumber,
                    $"Returned {form.ReturnedQuantity} unit(s) of {borrowing.Equipment.AssetCode} under {borrowing.TransactionNumber}." +
                    (string.IsNullOrWhiteSpace(form.Notes) ? string.Empty : " Notes: " + form.Notes.Trim()));

                return (true, null, returnEntity.ReturnId);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to record return for borrowing {BorrowingId}.", form?.BorrowingId);
                return (false, ErrorMessages.ServerError, 0);
            }
        }

        public (bool Success, string Error, long InspectionId) CreateInspection(InspectionFormViewModel form, IEnumerable<IFormFile> photos)
        {
            if (form == null || form.InspectedQuantity < 1)
            {
                return (false, ErrorMessages.ServerError, 0);
            }

            try
            {
                var borrowing = this._repository.GetBorrowingForReturn(form.BorrowingId);
                if (borrowing == null)
                {
                    return (false, ErrorMessages.ServerError, 0);
                }

                if (!this._currentUser.CanActOn(borrowing.Reservation?.AssignedCustodianUserId))
                {
                    return (false, ErrorMessages.CustodianNotAssigned, 0);
                }

                var inspectionStatus = this.DbContext.InspectionStatuses.FirstOrDefault(s => s.StatusId == form.InspectionStatusId);
                if (inspectionStatus == null)
                {
                    inspectionStatus = this.DbContext.InspectionStatuses.FirstOrDefault(s => s.Code == "PENDING");
                }

                var finalize = inspectionStatus?.Code == "COMPLETED";
                if (finalize)
                {
                    var returned = borrowing.BorrowingReturns.Sum(r => r.ReturnedQuantity);
                    var alreadyFinalized = this.DbContext.ConditionInspections
                        .Where(i => i.BorrowingId == borrowing.BorrowingId && i.InspectionStatus.Code == "COMPLETED")
                        .Sum(i => (int?)i.InspectedQuantity) ?? 0;
                    if (form.InspectedQuantity > returned - alreadyFinalized)
                    {
                        return (false, ErrorMessages.InspectionQuantityInvalid, 0);
                    }
                    if (form.GoodQuantity < 0 || form.DamagedQuantity < 0 || form.LostQuantity < 0
                        || form.GoodQuantity + form.DamagedQuantity + form.LostQuantity != form.InspectedQuantity)
                    {
                        return (false, ErrorMessages.InspectionOutcomeMismatch, 0);
                    }
                    if (form.RepairCost < 0 || form.PenaltyAmount < 0)
                    {
                        return (false, ErrorMessages.InspectionAmountInvalid, 0);
                    }
                }

                var inspection = new ConditionInspection
                {
                    BorrowingId = borrowing.BorrowingId,
                    BorrowingReturnId = form.BorrowingReturnId,
                    EquipmentId = borrowing.EquipmentId,
                    ReleaseConditionId = form.ReleaseConditionId,
                    CurrentConditionId = form.CurrentConditionId,
                    InspectedQuantity = form.InspectedQuantity,
                    InspectionNotes = form.InspectionNotes,
                    InspectedByUserId = this._currentUser.GetActorUserId() ?? borrowing.ReleasedByUserId,
                    InspectionStatusId = inspectionStatus?.StatusId ?? 1,
                    GoodQuantity = finalize ? form.GoodQuantity : null,
                    DamagedQuantity = finalize ? form.DamagedQuantity : null,
                    LostQuantity = finalize ? form.LostQuantity : null,
                    RepairCost = finalize ? form.RepairCost : null,
                    PenaltyAmount = finalize ? form.PenaltyAmount : null,
                    PenaltyNote = finalize ? form.PenaltyNote?.Trim() : null,
                };

                this.DbContext.ConditionInspections.Add(inspection);
                this._unitOfWork.SaveChanges();

                // photo upload (best-effort, capped at 6 files, 5 MB each)
                if (photos != null)
                {
                    var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "inspections", inspection.InspectionId.ToString());
                    Directory.CreateDirectory(dir);
                    var count = 0;
                    foreach (var file in photos)
                    {
                        if (count >= 6 || file == null || file.Length == 0)
                        {
                            continue;
                        }
                        if (file.Length > 5 * 1024 * 1024)
                        {
                            continue;
                        }
                        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                        if (!allowed.Contains(ext))
                        {
                            continue;
                        }
                        var safeName = $"photo_{count + 1}{ext}";
                        var path = Path.Combine(dir, safeName);
                        using (var stream = new FileStream(path, FileMode.Create))
                        {
                            file.CopyTo(stream);
                        }
                        count++;
                    }
                }

                // update equipment current condition
                var equipment = borrowing.Equipment;
                if (equipment != null)
                {
                    equipment.CurrentConditionId = form.CurrentConditionId;
                }

                if (finalize)
                {
                    this.ApplyInspectionOutcome(borrowing, form, inspection);
                }

                this._unitOfWork.SaveChanges();

                if (finalize)
                {
                    this.NotifyInspectionOutcome(borrowing, form);
                }

                this.WriteAudit("INSPECT", "ConditionInspection", inspection.InspectionId, borrowing.TransactionNumber,
                    $"Inspected {form.InspectedQuantity} unit(s) of {borrowing.Equipment.AssetCode}: condition={form.CurrentConditionId}, status={inspectionStatus?.Code}." +
                    (string.IsNullOrWhiteSpace(form.InspectionNotes) ? string.Empty : " Notes: " + form.InspectionNotes.Trim()));

                return (true, null, inspection.InspectionId);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to record inspection for borrowing {BorrowingId}.", form?.BorrowingId);
                return (false, ErrorMessages.ServerError, 0);
            }
        }

        /// <summary>
        /// Closes the inventory loop: units leave "awaiting inspection"; damaged ones go to
        /// maintenance and lost ones to unreconciled (good ones become available again).
        /// Marks the borrowing's inspection COMPLETED once every returned unit is finalized.
        /// </summary>
        private void ApplyInspectionOutcome(Borrowing borrowing, InspectionFormViewModel form, ConditionInspection inspection)
        {
            var balance = borrowing.Equipment?.EquipmentInventoryBalance;
            if (balance != null)
            {
                // Returns recorded before this change went straight to available, so only take
                // from the awaiting bucket what is actually there.
                balance.AwaitingInspectionQuantity -= Math.Min(form.InspectedQuantity, balance.AwaitingInspectionQuantity);
                balance.MaintenanceQuantity += form.DamagedQuantity;
                balance.UnreconciledQuantity += form.LostQuantity;
            }

            var actor = inspection.InspectedByUserId;
            void Move(int qty, string to)
            {
                if (qty <= 0)
                {
                    return;
                }

                this.DbContext.InventoryMovements.Add(new InventoryMovement
                {
                    EquipmentId = borrowing.EquipmentId,
                    MovementType = "INSPECTION",
                    FromState = "AWAITING_INSPECTION",
                    ToState = to,
                    Quantity = qty,
                    ReferenceNumber = borrowing.TransactionNumber,
                    PerformedByUserId = actor,
                });
            }

            Move(form.GoodQuantity, "AVAILABLE");
            Move(form.DamagedQuantity, "MAINTENANCE");
            Move(form.LostQuantity, "UNRECONCILED");

            var returned = borrowing.BorrowingReturns.Sum(r => r.ReturnedQuantity);
            var finalizedBefore = this.DbContext.ConditionInspections
                .Where(i => i.BorrowingId == borrowing.BorrowingId && i.InspectionId != inspection.InspectionId && i.InspectionStatus.Code == "COMPLETED")
                .Sum(i => (int?)i.InspectedQuantity) ?? 0;
            var allReturned = returned >= borrowing.ReleasedQuantity;
            if (allReturned && finalizedBefore + form.InspectedQuantity >= returned)
            {
                var completed = this.DbContext.InspectionStatuses.FirstOrDefault(s => s.Code == "COMPLETED");
                if (completed != null)
                {
                    borrowing.InspectionStatusId = completed.StatusId;
                }
            }
        }

        private void NotifyInspectionOutcome(Borrowing borrowing, InspectionFormViewModel form)
        {
            var item = borrowing.Equipment?.EquipmentName ?? "equipment";
            var parts = new List<string> { $"{form.GoodQuantity} in good condition" };
            if (form.DamagedQuantity > 0) parts.Add($"{form.DamagedQuantity} damaged");
            if (form.LostQuantity > 0) parts.Add($"{form.LostQuantity} lost");
            var charges = form.PenaltyAmount > 0 ? $" A penalty of {form.PenaltyAmount:N2} was recorded" + (string.IsNullOrWhiteSpace(form.PenaltyNote) ? "." : $": {form.PenaltyNote.Trim()}") : string.Empty;

            this.WriteNotification(borrowing.BorrowerUserId, "Return inspection completed",
                $"Inspection of {item} ({borrowing.TransactionNumber}) is complete: {string.Join(", ", parts)}.{charges}");

            if (form.DamagedQuantity + form.LostQuantity > 0)
            {
                var staff = this._notifications.GetActiveUserIdsInRoles(RoleCodes.Admin);
                if (borrowing.Reservation?.AssignedCustodianUserId is long custodianId)
                {
                    staff.Add(custodianId);
                }
                else
                {
                    staff.AddRange(this._notifications.GetActiveUserIdsInRoles(RoleCodes.Technician));
                }

                this._notifications.Notify(staff, NotificationTypes.Alert,
                    $"Damage/loss recorded: {borrowing.TransactionNumber}",
                    $"{item}: {form.DamagedQuantity} damaged (moved to maintenance), {form.LostQuantity} lost (unreconciled)." +
                    (form.RepairCost > 0 ? $" Estimated repair cost {form.RepairCost:N2}." : string.Empty));
            }
        }

        private void WriteNotification(long recipientUserId, string title, string message)
        {
            this._notifications.Notify(recipientUserId, NotificationTypes.Return, title, message);
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
