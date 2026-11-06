using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public interface IReturnProcessingRepository
    {
        Borrowing GetBorrowingForReturn(long borrowingId);

        PagedResult<BorrowingReturn> GetReturnsPaged(string search, int page, int pageSize, long? scopeCustodianUserId = null);

        BorrowingReturn GetReturnWithDetails(long returnId);
    }

    public class ReturnProcessingRepository : BaseRepository, IReturnProcessingRepository
    {
        public ReturnProcessingRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public Borrowing GetBorrowingForReturn(long borrowingId)
        {
            return this.Context.Borrowings
                .Include(b => b.BorrowerUser).ThenInclude(u => u.Role)
                .Include(b => b.Equipment).ThenInclude(e => e.EquipmentCategory)
                .Include(b => b.Equipment).ThenInclude(e => e.EquipmentInventoryBalance)
                .Include(b => b.Status)
                .Include(b => b.InspectionStatus)
                .Include(b => b.Reservation)
                .Include(b => b.BorrowingReturns)
                .FirstOrDefault(b => b.BorrowingId == borrowingId);
        }

        public PagedResult<BorrowingReturn> GetReturnsPaged(string search, int page, int pageSize, long? scopeCustodianUserId = null)
        {
            const int DefaultPage = 1;
            const int DefaultPageSize = 10;

            var query = this.Context.BorrowingReturns
                .Include(r => r.Borrowing).ThenInclude(b => b.Equipment)
                .Include(r => r.Borrowing).ThenInclude(b => b.BorrowerUser)
                .Include(r => r.ReceivedByUser)
                .AsNoTracking();

            if (scopeCustodianUserId.HasValue)
            {
                var custodianId = scopeCustodianUserId.Value;
                query = query.Where(r => r.Borrowing.Reservation == null
                    || r.Borrowing.Reservation.AssignedCustodianUserId == null
                    || r.Borrowing.Reservation.AssignedCustodianUserId == custodianId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(r =>
                    r.Borrowing.Equipment.EquipmentName.Contains(term) ||
                    r.Borrowing.Equipment.AssetCode.Contains(term) ||
                    r.Borrowing.BorrowerUser.FirstName.Contains(term) ||
                    r.Borrowing.BorrowerUser.LastName.Contains(term));
            }

            var totalCount = query.Count();

            page = page < 1 ? DefaultPage : page;
            pageSize = pageSize < 1 ? DefaultPageSize : pageSize;

            var items = query
                .OrderByDescending(r => r.ReturnedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<BorrowingReturn>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
        }

        public BorrowingReturn GetReturnWithDetails(long returnId)
        {
            return this.Context.BorrowingReturns
                .Include(r => r.Borrowing).ThenInclude(b => b.Equipment)
                .Include(r => r.Borrowing).ThenInclude(b => b.BorrowerUser)
                .Include(r => r.Borrowing).ThenInclude(b => b.Reservation)
                .Include(r => r.ReceivedByUser)
                .Include(r => r.ConditionInspections)
                .FirstOrDefault(r => r.ReturnId == returnId);
        }
    }
}
