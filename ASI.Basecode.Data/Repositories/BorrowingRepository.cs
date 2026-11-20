using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    /// <summary>
    /// Borrowing repository implementation. Data access only.
    /// </summary>
    public class BorrowingRepository : BaseRepository, IBorrowingRepository
    {
        public BorrowingRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public int CountForBorrowerWithStatus(long borrowerUserId, string statusCode)
        {
            return this.Context.Borrowings
                .AsNoTracking()
                .Count(b => b.BorrowerUserId == borrowerUserId && b.Status.Code == statusCode);
        }

        public int CountForBorrowerWithoutStatuses(long borrowerUserId, string firstStatusCode, string secondStatusCode)
        {
            return this.Context.Borrowings
                .AsNoTracking()
                .Count(b => b.BorrowerUserId == borrowerUserId
                    && b.Status.Code != firstStatusCode
                    && b.Status.Code != secondStatusCode);
        }

        public int CountForBorrowerInStatusesDueBefore(long borrowerUserId, IReadOnlyCollection<string> statusCodes, DateTime dueBeforeUtc)
        {
            return this.Context.Borrowings
                .AsNoTracking()
                .Count(b => b.BorrowerUserId == borrowerUserId
                    && statusCodes.Contains(b.Status.Code)
                    && b.DueAtUtc < dueBeforeUtc);
        }
    
        public List<Borrowing> GetForBorrowerExcludingStatuses(long borrowerUserId, string firstExcludedStatusCode, string secondExcludedStatusCode, string search)
        {
            var query = this.Context.Borrowings
                .AsNoTracking()
                .Include(b => b.Equipment)
                .Include(b => b.Status)
                .Include(b => b.ReleaseCondition)
                .Where(b => b.BorrowerUserId == borrowerUserId
                    && b.Status.Code != firstExcludedStatusCode
                    && b.Status.Code != secondExcludedStatusCode);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(b =>
                    b.TransactionNumber.Contains(term) ||
                    b.Equipment.EquipmentName.Contains(term) ||
                    b.Equipment.AssetCode.Contains(term));
            }

            return query
                .OrderBy(b => b.DueAtUtc)
                .ThenBy(b => b.BorrowingId)
                .ToList();
        }

        public Borrowing GetDetailsForBorrower(long borrowingId, long borrowerUserId)
        {
            return this.Context.Borrowings
                .AsNoTracking()
                .Include(x => x.Equipment).ThenInclude(e => e.EquipmentCategory)
                .Include(x => x.Equipment).ThenInclude(e => e.Location)
                .Include(x => x.Status)
                .Include(x => x.ReleaseCondition)
                .Include(x => x.ReleasedByUser)
                .Include(x => x.BorrowingReturns).ThenInclude(r => r.ReceivedByUser)
                .Include(x => x.ConditionInspections).ThenInclude(i => i.CurrentCondition)
                .Include(x => x.ConditionInspections).ThenInclude(i => i.InspectionStatus)
                .FirstOrDefault(x => x.BorrowingId == borrowingId && x.BorrowerUserId == borrowerUserId);
        }

        public List<Borrowing> GetHistoryForBorrower(long borrowerUserId, string firstStatusCode, string secondStatusCode, string search)
        {
            var query = this.Context.Borrowings
                .AsNoTracking()
                .Include(b => b.Equipment)
                .Include(b => b.Status)
                .Include(b => b.BorrowingReturns)
                .Include(b => b.ConditionInspections).ThenInclude(i => i.CurrentCondition)
                .Where(b => b.BorrowerUserId == borrowerUserId && (b.Status.Code == firstStatusCode || b.Status.Code == secondStatusCode));

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(b =>
                    b.TransactionNumber.Contains(term) ||
                    b.Equipment.EquipmentName.Contains(term) ||
                    b.Equipment.AssetCode.Contains(term));
            }

            return query
                .OrderByDescending(b => b.BorrowingReturns.OrderByDescending(r => r.ReturnedAtUtc).Select(r => r.ReturnedAtUtc).FirstOrDefault())
                .ThenByDescending(b => b.BorrowingId)
                .ToList();
        }

        public List<Borrowing> GetTrackedInStatusesDueBefore(IReadOnlyCollection<string> statusCodes, DateTime dueBeforeUtc)
        {
            return this.Context.Borrowings
                .Include(b => b.Equipment)
                .Include(b => b.Reservation)
                .Where(b => statusCodes.Contains(b.Status.Code) && b.DueAtUtc < dueBeforeUtc)
                .ToList();
        }

        public List<Borrowing> GetTrackedInStatusNotOverdueNotified(string statusCode)
        {
            return this.Context.Borrowings
                .Include(b => b.Equipment)
                .Include(b => b.Reservation)
                .Where(b => b.Status.Code == statusCode && b.OverdueNotifiedAtUtc == null)
                .ToList();
        }

        public List<Borrowing> GetTrackedDueForReminder(IReadOnlyCollection<string> statusCodes, DateTime fromUtc, DateTime toUtc)
        {
            return this.Context.Borrowings
                .Include(b => b.Equipment)
                .Where(b => statusCodes.Contains(b.Status.Code)
                    && b.DueReminderSentAtUtc == null
                    && b.DueAtUtc >= fromUtc
                    && b.DueAtUtc <= toUtc)
                .ToList();
        }

        public List<Borrowing> GetInCustodianScope(long? scopeCustodianUserId)
        {
            var scope = scopeCustodianUserId;
            return this.Context.Borrowings
                .AsNoTracking()
                .Include(b => b.BorrowerUser)
                .Include(b => b.Equipment)
                .Include(b => b.Status)
                .Include(b => b.InspectionStatus)
                .Where(b => scope == null || b.Reservation == null
                    || b.Reservation.AssignedCustodianUserId == null
                    || b.Reservation.AssignedCustodianUserId == scope)
                .ToList();
        }

        public List<Borrowing> GetAllForBorrower(long borrowerUserId)
        {
            return this.Context.Borrowings.AsNoTracking()
                .Include(b => b.Equipment)
                .Include(b => b.Status)
                .Include(b => b.BorrowingReturns)
                .Where(b => b.BorrowerUserId == borrowerUserId)
                .OrderByDescending(b => b.ReleasedAtUtc)
                .ToList();
        }
    }

}
