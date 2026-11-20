using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ViewModelClasses;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Borrower borrowings (2.6), borrowing details (2.7) and borrowing history (2.8).
    /// </summary>
    public class BorrowingService : ServiceBase, IBorrowingService
    {
        private readonly IBorrowingRepository _borrowings;
        private readonly ISettingsRepository _settings;

        public BorrowingService(ILoggerFactory loggerFactory, IBorrowingRepository borrowings, ISettingsRepository settings)
            : base(loggerFactory)
        {
            this._borrowings = borrowings;
            this._settings = settings;
        }

        public BorrowingMineViewModel GetMine(long userId, string search)
        {
            var now = DateTime.UtcNow;
            var cutoff = BorrowingRules.GetOverdueCutoffUtc(this._settings, now);

            var rows = this._borrowings.GetForBorrowerExcludingStatuses(userId, BorrowingStatusCodes.Returned, BorrowingStatusCodes.Lost, search);

            return new BorrowingMineViewModel
            {
                Search = search,
                Borrowings = rows.Select(b =>
                {
                    var isOverdue = BorrowingRules.IsOverdue(b.Status.Code, b.DueAtUtc, cutoff);
                    return new BorrowingListRowViewModel
                    {
                        BorrowingId = b.BorrowingId,
                        TransactionNumber = b.TransactionNumber,
                        EquipmentName = b.Equipment?.EquipmentName,
                        AssetCode = b.Equipment?.AssetCode,
                        Quantity = b.ReleasedQuantity,
                        ReleasedAtUtc = b.ReleasedAtUtc,
                        DueAtUtc = b.DueAtUtc,
                        ReleaseConditionName = b.ReleaseCondition?.Description,
                        StatusCode = isOverdue ? BorrowingStatusCodes.Overdue : b.Status.Code,
                        StatusDescription = isOverdue ? "Overdue" : b.Status.Description,
                        IsOverdue = isOverdue,
                        IsDueToday = BorrowingRules.IsLocalToday(b.DueAtUtc, now),
                        DaysOverdue = isOverdue ? BorrowingRules.DaysOverdue(b.DueAtUtc, now) : 0,
                    };
                }).ToList(),
            };
        }

        public BorrowingDetailsViewModel GetDetails(long userId, long borrowingId)
        {
            var now = DateTime.UtcNow;

            var b = this._borrowings.GetDetailsForBorrower(borrowingId, userId);

            if (b == null)
            {
                return null;
            }

            var returned = string.Equals(b.Status?.Code, BorrowingStatusCodes.Returned, StringComparison.OrdinalIgnoreCase)
                || string.Equals(b.Status?.Code, BorrowingStatusCodes.Lost, StringComparison.OrdinalIgnoreCase);
            var isOverdue = BorrowingRules.IsOverdue(b.Status?.Code, b.DueAtUtc, BorrowingRules.GetOverdueCutoffUtc(this._settings, now));
            var lastReturn = b.BorrowingReturns?.OrderByDescending(r => r.ReturnedAtUtc).FirstOrDefault();
            var totalReturned = b.BorrowingReturns?.Sum(r => r.ReturnedQuantity) ?? 0;
            var lastInspection = b.ConditionInspections?.OrderByDescending(i => i.CreatedAtUtc).FirstOrDefault();
            // Charges from finished inspections only; a mixed return can be inspected in several batches.
            var charged = b.ConditionInspections?
                .Where(i => i.InspectionStatus?.Code == InspectionStatusCodes.Completed && i.PenaltyAmount > 0)
                .OrderBy(i => i.CreatedAtUtc).ToList() ?? new List<ConditionInspection>();

            return new BorrowingDetailsViewModel
            {
                BorrowingId = b.BorrowingId,
                TransactionNumber = b.TransactionNumber,
                EquipmentName = b.Equipment?.EquipmentName,
                AssetCode = b.Equipment?.AssetCode,
                CategoryName = b.Equipment?.EquipmentCategory?.CategoryName,
                LocationName = b.Equipment?.Location?.LocationName,
                Quantity = b.ReleasedQuantity,
                ReleasedAtUtc = b.ReleasedAtUtc,
                ReleasedBy = b.ReleasedByUser == null ? null : $"{b.ReleasedByUser.FirstName} {b.ReleasedByUser.LastName}".Trim(),
                ReleaseConditionName = b.ReleaseCondition?.Description,
                DueAtUtc = b.DueAtUtc,
                StatusCode = isOverdue ? BorrowingStatusCodes.Overdue : b.Status?.Code,
                StatusDescription = isOverdue ? "Overdue" : b.Status?.Description,
                IsOverdue = isOverdue,
                IsReturned = returned,
                DaysOverdue = isOverdue ? BorrowingRules.DaysOverdue(b.DueAtUtc, now) : 0,
                ReturnedAtUtc = lastReturn?.ReturnedAtUtc,
                // Total across every partial return, not just the latest one.
                ReturnedQuantity = lastReturn == null ? null : totalReturned,
                ReceivedBy = lastReturn?.ReceivedByUser == null ? null : $"{lastReturn.ReceivedByUser.FirstName} {lastReturn.ReceivedByUser.LastName}".Trim(),
                FinalConditionName = lastInspection?.CurrentCondition?.Description,
                InspectionNotes = lastInspection?.InspectionNotes,
                PenaltyAmount = charged.Count == 0 ? null : charged.Sum(i => i.PenaltyAmount),
                PenaltyNote = string.Join(" ", charged.Select(i => i.PenaltyNote).Where(n => !string.IsNullOrWhiteSpace(n))),
            };
        }

        public BorrowingHistoryViewModel GetHistory(long userId, string search, string condition)
        {
            var rows = this._borrowings.GetHistoryForBorrower(userId, BorrowingStatusCodes.Returned, BorrowingStatusCodes.Lost, search)
                .Select(b =>
                {
                    var lastReturn = b.BorrowingReturns?.OrderByDescending(r => r.ReturnedAtUtc).FirstOrDefault();
                    var lastInspection = b.ConditionInspections?.OrderByDescending(i => i.CreatedAtUtc).FirstOrDefault();
                    return new BorrowingHistoryRowViewModel
                    {
                        BorrowingId = b.BorrowingId,
                        TransactionNumber = b.TransactionNumber,
                        EquipmentName = b.Equipment?.EquipmentName,
                        AssetCode = b.Equipment?.AssetCode,
                        ReleasedAtUtc = b.ReleasedAtUtc,
                        ReturnedAtUtc = lastReturn?.ReturnedAtUtc,
                        FinalConditionName = lastInspection?.CurrentCondition?.Description,
                        InspectionNotes = lastInspection?.InspectionNotes,
                        StatusDescription = b.Status?.Description,
                    };
                })
                .ToList();

            if (!string.IsNullOrWhiteSpace(condition) && condition != "All")
            {
                rows = rows.Where(r => string.Equals(r.FinalConditionName, condition, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return new BorrowingHistoryViewModel
            {
                Search = search,
                Condition = string.IsNullOrWhiteSpace(condition) ? "All" : condition,
                Borrowings = rows,
                ConditionOptions = new List<string> { "All", "Good", "Minor Damage", "Damaged", "Missing Parts", "Lost" },
            };
        }
    }
}
