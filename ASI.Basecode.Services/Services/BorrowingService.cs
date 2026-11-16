using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ViewModelClasses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Borrower borrowings (2.6), borrowing details (2.7) and borrowing history (2.8).
    /// </summary>
    public class BorrowingService : ServiceBase, IBorrowingService
    {
        private readonly IUnitOfWork _unitOfWork;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public BorrowingService(ILoggerFactory loggerFactory, IUnitOfWork unitOfWork)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
        }

        public BorrowingMineViewModel GetMine(long userId, string search)
        {
            var now = DateTime.UtcNow;
            var cutoff = BorrowingRules.GetOverdueCutoffUtc(this.DbContext, now);

            var query = this.DbContext.Borrowings
                .AsNoTracking()
                .Include(b => b.Equipment)
                .Include(b => b.Status)
                .Include(b => b.ReleaseCondition)
                .Where(b => b.BorrowerUserId == userId
                    && b.Status.Code != "RETURNED"
                    && b.Status.Code != "LOST");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(b =>
                    b.TransactionNumber.Contains(term) ||
                    b.Equipment.EquipmentName.Contains(term) ||
                    b.Equipment.AssetCode.Contains(term));
            }

            var rows = query
                .OrderBy(b => b.DueAtUtc)
                .ThenBy(b => b.BorrowingId)
                .ToList();

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
                        StatusCode = isOverdue ? "OVERDUE" : b.Status.Code,
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

            var b = this.DbContext.Borrowings
                .AsNoTracking()
                .Include(x => x.Equipment).ThenInclude(e => e.EquipmentCategory)
                .Include(x => x.Equipment).ThenInclude(e => e.Location)
                .Include(x => x.Status)
                .Include(x => x.ReleaseCondition)
                .Include(x => x.ReleasedByUser)
                .Include(x => x.BorrowingReturns).ThenInclude(r => r.ReceivedByUser)
                .Include(x => x.ConditionInspections).ThenInclude(i => i.CurrentCondition)
                .FirstOrDefault(x => x.BorrowingId == borrowingId && x.BorrowerUserId == userId);

            if (b == null)
            {
                return null;
            }

            var returned = string.Equals(b.Status?.Code, "RETURNED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(b.Status?.Code, "LOST", StringComparison.OrdinalIgnoreCase);
            var isOverdue = BorrowingRules.IsOverdue(b.Status?.Code, b.DueAtUtc, BorrowingRules.GetOverdueCutoffUtc(this.DbContext, now));
            var lastReturn = b.BorrowingReturns?.OrderByDescending(r => r.ReturnedAtUtc).FirstOrDefault();
            var totalReturned = b.BorrowingReturns?.Sum(r => r.ReturnedQuantity) ?? 0;
            var lastInspection = b.ConditionInspections?.OrderByDescending(i => i.CreatedAtUtc).FirstOrDefault();

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
                StatusCode = isOverdue ? "OVERDUE" : b.Status?.Code,
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
            };
        }

        public BorrowingHistoryViewModel GetHistory(long userId, string search, string condition)
        {
            var query = this.DbContext.Borrowings
                .AsNoTracking()
                .Include(b => b.Equipment)
                .Include(b => b.Status)
                .Include(b => b.BorrowingReturns)
                .Include(b => b.ConditionInspections).ThenInclude(i => i.CurrentCondition)
                .Where(b => b.BorrowerUserId == userId && (b.Status.Code == "RETURNED" || b.Status.Code == "LOST"));

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(b =>
                    b.TransactionNumber.Contains(term) ||
                    b.Equipment.EquipmentName.Contains(term) ||
                    b.Equipment.AssetCode.Contains(term));
            }

            var rows = query
                .OrderByDescending(b => b.BorrowingReturns.OrderByDescending(r => r.ReturnedAtUtc).Select(r => r.ReturnedAtUtc).FirstOrDefault())
                .ThenByDescending(b => b.BorrowingId)
                .ToList()
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
