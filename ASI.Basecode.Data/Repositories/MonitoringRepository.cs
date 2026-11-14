using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public interface IMonitoringRepository
    {
        (int totalReservations, int pending, int approved, int rejected, int cancelled, int completed, int expired) GetReservationCounts();

        (int totalBorrowings, int active, int partiallyReturned, int returned, int overdue, int lost) GetBorrowingCounts(DateTime overdueCutoffUtc);

        (int totalEquipment, int totalUnits, int availableUnits) GetEquipmentCounts();

        PagedResult<AuditLog> GetAuditLogsPaged(string search, string entityType, int page, int pageSize);

        PagedResult<InventoryMovement> GetMovementsPaged(string search, int page, int pageSize);
    }

    public class MonitoringRepository : BaseRepository, IMonitoringRepository
    {
        public MonitoringRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public (int totalReservations, int pending, int approved, int rejected, int cancelled, int completed, int expired) GetReservationCounts()
        {
            var q = this.Context.ReservationStatuses.AsNoTracking().ToList();
            int Code(string c) => (int)(q.FirstOrDefault(s => s.Code == c)?.StatusId ?? -1);
            var p = Code("PENDING"); var a = Code("APPROVED"); var r = Code("REJECTED");
            var c = Code("CANCELLED"); var comp = Code("COMPLETED"); var e = Code("EXPIRED");
            var all = this.Context.Reservations.AsNoTracking();
            return (all.Count(),
                all.Count(x => x.StatusId == p),
                all.Count(x => x.StatusId == a),
                all.Count(x => x.StatusId == r),
                all.Count(x => x.StatusId == c),
                all.Count(x => x.StatusId == comp),
                all.Count(x => x.StatusId == e));
        }

        /// <param name="overdueCutoffUtc">Now minus the policy grace period: open borrowings due before it are overdue.</param>
        public (int totalBorrowings, int active, int partiallyReturned, int returned, int overdue, int lost) GetBorrowingCounts(DateTime overdueCutoffUtc)
        {
            var q = this.Context.BorrowingStatuses.AsNoTracking().ToList();
            int Code(string c) => (int)(q.FirstOrDefault(s => s.Code == c)?.StatusId ?? -1);
            var active = Code("ACTIVE"); var partial = Code("PARTIALLY_RETURNED");
            var returned = Code("RETURNED"); var overdue = Code("OVERDUE"); var lost = Code("LOST");
            var all = this.Context.Borrowings.AsNoTracking();
            return (all.Count(),
                all.Count(x => x.StatusId == active),
                all.Count(x => x.StatusId == partial),
                all.Count(x => x.StatusId == returned),
                all.Count(x => (x.StatusId == active || x.StatusId == partial || x.StatusId == overdue) && x.DueAtUtc < overdueCutoffUtc),
                all.Count(x => x.StatusId == lost));
        }

        public (int totalEquipment, int totalUnits, int availableUnits) GetEquipmentCounts()
        {
            var totals = this.Context.EquipmentInventoryBalances.AsNoTracking().ToList();
            return (this.Context.Equipment.Count(),
                totals.Sum(b => b.TotalQuantity),
                totals.Sum(b => b.TotalQuantity - b.ReservedQuantity - b.ReleasedQuantity - b.AwaitingInspectionQuantity - b.MaintenanceQuantity - b.UnreconciledQuantity));
        }

        public PagedResult<AuditLog> GetAuditLogsPaged(string search, string entityType, int page, int pageSize)
        {
            const int DefaultPage = 1;
            const int DefaultPageSize = 20;

            var query = this.Context.AuditLogs
                .Include(a => a.ActorUser)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(entityType) && entityType != "All")
            {
                query = query.Where(a => a.EntityType == entityType);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(a =>
                    (a.Details != null && a.Details.Contains(term)) ||
                    (a.ReferenceNumber != null && a.ReferenceNumber.Contains(term)) ||
                    (a.ActionType != null && a.ActionType.Contains(term)));
            }

            var totalCount = query.Count();

            page = page < 1 ? DefaultPage : page;
            pageSize = pageSize < 1 ? DefaultPageSize : pageSize;

            var items = query
                .OrderByDescending(a => a.OccurredAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<AuditLog>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
        }

        public PagedResult<InventoryMovement> GetMovementsPaged(string search, int page, int pageSize)
        {
            const int DefaultPage = 1;
            const int DefaultPageSize = 20;

            var query = this.Context.InventoryMovements
                .Include(m => m.Equipment)
                .Include(m => m.PerformedByUser)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(m =>
                    (m.Equipment != null && m.Equipment.EquipmentName.Contains(term)) ||
                    (m.ReferenceNumber != null && m.ReferenceNumber.Contains(term)) ||
                    (m.MovementType != null && m.MovementType.Contains(term)));
            }

            var totalCount = query.Count();

            page = page < 1 ? DefaultPage : page;
            pageSize = pageSize < 1 ? DefaultPageSize : pageSize;

            var items = query
                .OrderByDescending(m => m.OccurredAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<InventoryMovement>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
        }
    }
}
