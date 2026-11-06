using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class CustodianRepository : BaseRepository, ICustodianRepository
    {
        public CustodianRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public PagedResult<Reservation> GetReservationsPaged(string search, long? statusId, int page, int pageSize, long? scopeCustodianUserId = null)
        {
            const int DefaultPage = 1;
            const int DefaultPageSize = 10;

            var query = this.Context.Reservations
                .Include(r => r.BorrowerUser)
                .Include(r => r.Equipment).ThenInclude(e => e.EquipmentCategory)
                .Include(r => r.Equipment).ThenInclude(e => e.Location)
                .Include(r => r.Status)
                .Include(r => r.PickupLocation)
                .Include(r => r.AssignedCustodianUser)
                .AsNoTracking();

            // Custodians see their routed work plus the unassigned pool; admins pass null.
            if (scopeCustodianUserId.HasValue)
            {
                var custodianId = scopeCustodianUserId.Value;
                query = query.Where(r => r.AssignedCustodianUserId == null || r.AssignedCustodianUserId == custodianId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim()}%";
                query = query.Where(r =>
                    EF.Functions.Like(r.ReservationNumber, pattern) ||
                    EF.Functions.Like(r.BorrowerUser.FirstName + " " + r.BorrowerUser.LastName, pattern) ||
                    EF.Functions.Like(r.Equipment.EquipmentName, pattern) ||
                    EF.Functions.Like(r.Equipment.AssetCode, pattern));
            }

            if (statusId.HasValue)
            {
                query = query.Where(r => r.StatusId == statusId.Value);
            }

            var totalCount = query.Count();

            page = page < 1 ? DefaultPage : page;
            pageSize = pageSize < 1 ? DefaultPageSize : pageSize;

            var items = query
                .OrderByDescending(r => r.SubmittedAtUtc)
                .ThenByDescending(r => r.ReservationId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<Reservation>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
        }

        public Reservation GetReservationWithDetails(long reservationId)
        {
            return this.Context.Reservations
                .Include(r => r.BorrowerUser).ThenInclude(u => u.Role)
                .Include(r => r.BorrowerUser).ThenInclude(u => u.Department)
                .Include(r => r.BorrowerUser).ThenInclude(u => u.AccountStatus)
                .Include(r => r.Equipment).ThenInclude(e => e.EquipmentCategory)
                .Include(r => r.Equipment).ThenInclude(e => e.Location)
                .Include(r => r.Equipment).ThenInclude(e => e.CurrentCondition)
                .Include(r => r.Equipment).ThenInclude(e => e.EquipmentInventoryBalance)
                .Include(r => r.PickupLocation)
                .Include(r => r.Status)
                .Include(r => r.AssignedCustodianUser)
                .FirstOrDefault(r => r.ReservationId == reservationId);
        }

        public PagedResult<Borrowing> GetActiveBorrowingsPaged(string search, int page, int pageSize, long? scopeCustodianUserId = null)
        {
            const int DefaultPage = 1;
            const int DefaultPageSize = 10;

            var query = this.Context.Borrowings
                .Include(b => b.BorrowerUser)
                .Include(b => b.Equipment).ThenInclude(e => e.EquipmentCategory)
                .Include(b => b.ReleasedByUser)
                .Include(b => b.Status)
                .Include(b => b.InspectionStatus)
                .AsNoTracking()
                .Where(b => b.Status.Code == "ACTIVE" || b.Status.Code == "PARTIALLY_RETURNED" || b.Status.Code == "OVERDUE");

            if (scopeCustodianUserId.HasValue)
            {
                var custodianId = scopeCustodianUserId.Value;
                query = query.Where(b => b.Reservation == null
                    || b.Reservation.AssignedCustodianUserId == null
                    || b.Reservation.AssignedCustodianUserId == custodianId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(b =>
                    b.BorrowerUser.FirstName.Contains(term) ||
                    b.BorrowerUser.LastName.Contains(term) ||
                    b.Equipment.EquipmentName.Contains(term) ||
                    b.Equipment.AssetCode.Contains(term) ||
                    b.TransactionNumber.Contains(term));
            }

            var totalCount = query.Count();

            page = page < 1 ? DefaultPage : page;
            pageSize = pageSize < 1 ? DefaultPageSize : pageSize;

            var items = query
                .OrderByDescending(b => b.ReleasedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<Borrowing>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
        }

        public int CountActiveBorrowingsForReservation(long reservationId)
        {
            return this.Context.Borrowings.Count(b => b.ReservationId == reservationId);
        }

        public int SumReleasedForReservation(long reservationId)
        {
            return this.Context.Borrowings
                .Where(b => b.ReservationId == reservationId)
                .Sum(b => (int?)b.ReleasedQuantity) ?? 0;
        }

        public string GetNextTransactionNumber(int year)
        {
            var prefix = $"TXN-{year}-";
            var last = this.Context.Borrowings
                .Where(b => b.TransactionNumber.StartsWith(prefix))
                .OrderByDescending(b => b.TransactionNumber)
                .Select(b => b.TransactionNumber)
                .FirstOrDefault();

            var next = 1;
            if (!string.IsNullOrEmpty(last))
            {
                var tail = last.Substring(prefix.Length);
                if (int.TryParse(tail, out var n))
                {
                    next = n + 1;
                }
            }

            return $"{prefix}{next:D4}";
        }

        public Borrowing GetBorrowingWithDetails(long borrowingId)
        {
            return this.Context.Borrowings
                .Include(b => b.BorrowerUser).ThenInclude(u => u.Role)
                .Include(b => b.Equipment).ThenInclude(e => e.EquipmentCategory)
                .Include(b => b.ReleasedByUser)
                .Include(b => b.Status)
                .Include(b => b.InspectionStatus)
                .FirstOrDefault(b => b.BorrowingId == borrowingId);
        }
    }
}
