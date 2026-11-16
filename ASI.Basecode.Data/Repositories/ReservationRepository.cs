using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using ASI.Basecode.Data.QueryModels;

namespace ASI.Basecode.Data.Repositories
{
    /// <summary>
    /// Reservation repository implementation. Queries only — business rules live in services.
    /// </summary>
    public class ReservationRepository : BaseRepository, IReservationRepository
    {
        public ReservationRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public PagedResult<Equipment> GetAvailableEquipmentPaged(
            string search,
            long? categoryId,
            int page,
            int pageSize)
        {
            const int DefaultPage = 1;
            const int DefaultPageSize = 10;

            var query = this.Context.Equipment
                .Include(e => e.EquipmentCategory)
                .Include(e => e.Location)
                .Include(e => e.CurrentCondition)
                .Include(e => e.OperationalStatus)
                .Include(e => e.EquipmentInventoryBalance)
                .AsNoTracking()
                .Where(e => e.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim()}%";
                query = query.Where(e =>
                    EF.Functions.Like(e.EquipmentName, pattern) ||
                    EF.Functions.Like(e.AssetCode, pattern) ||
                    EF.Functions.Like(e.EquipmentCategory.CategoryName, pattern));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(e => e.EquipmentCategoryId == categoryId.Value);
            }

            var totalCount = query.Count();

            page = page < 1 ? DefaultPage : page;
            pageSize = pageSize < 1 ? DefaultPageSize : pageSize;

            var items = query
                .OrderBy(e => e.EquipmentName)
                .ThenBy(e => e.EquipmentId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<Equipment>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
        }

        public Equipment GetReservableEquipment(long equipmentId)
        {
            return this.Context.Equipment
                .Include(e => e.EquipmentCategory)
                .Include(e => e.Location)
                .Include(e => e.CurrentCondition)
                .Include(e => e.OperationalStatus)
                .Include(e => e.EquipmentInventoryBalance)
                .AsNoTracking()
                .FirstOrDefault(e => e.EquipmentId == equipmentId && e.IsActive);
        }

        public Reservation GetByIdWithDetails(long reservationId)
        {
            return this.Context.Reservations
                .Include(r => r.Equipment)
                    .ThenInclude(e => e.EquipmentCategory)
                .Include(r => r.BorrowerUser)
                    .ThenInclude(u => u.Role)
                .Include(r => r.PickupLocation)
                .Include(r => r.Status)
                .Include(r => r.BorrowingPolicyVersion)
                .AsNoTracking()
                .FirstOrDefault(r => r.ReservationId == reservationId);
        }

        public Reservation GetTrackedById(long reservationId)
        {
            return this.Context.Reservations
                .FirstOrDefault(r => r.ReservationId == reservationId);
        }

        public List<Reservation> GetForBorrower(long borrowerUserId)
        {
            return this.Context.Reservations
                .Include(r => r.Equipment)
                    .ThenInclude(e => e.EquipmentCategory)
                .Include(r => r.PickupLocation)
                .Include(r => r.Status)
                .AsNoTracking()
                .Where(r => r.BorrowerUserId == borrowerUserId)
                .OrderByDescending(r => r.SubmittedAtUtc)
                .ThenByDescending(r => r.ReservationId)
                .ToList();
        }

        public bool AnyAssignedToCustodianInStatuses(long custodianUserId, IReadOnlyCollection<string> statusCodes)
        {
            return this.Context.Reservations.Any(r => r.AssignedCustodianUserId == custodianUserId
                && statusCodes.Contains(r.Status.Code));
        }

        public List<Reservation> GetTrackedStale(string pendingStatusCode, string approvedStatusCode, DateTime submittedBefore, DateTime pickupBefore, DateTime nowUtc)
        {
            return this.Context.Reservations
                .Include(r => r.Status)
                .Include(r => r.Equipment).ThenInclude(e => e.EquipmentInventoryBalance)
                .Where(r => (r.Status.Code == pendingStatusCode && (r.SubmittedAtUtc < submittedBefore || r.RequestedStartAtUtc < nowUtc))
                    || (r.Status.Code == approvedStatusCode && r.RequestedStartAtUtc < pickupBefore))
                .ToList();
        }

        public List<Reservation> GetTrackedWithoutExpiryWarning(string statusCode)
        {
            return this.Context.Reservations
                .Include(r => r.Equipment)
                .Include(r => r.BorrowerUser)
                .Where(r => r.Status.Code == statusCode && r.ExpiryWarningSentAtUtc == null)
                .ToList();
        }
    }
}
