using System.Collections.Generic;
using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using ASI.Basecode.Data.QueryModels;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Repositories
{
    public class MonitoringRepository : BaseRepository, IMonitoringRepository
    {
        public MonitoringRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public (int totalReservations, int pending, int approved, int rejected, int cancelled, int completed, int expired) GetReservationCounts()
        {
            var q = this.Context.ReservationStatuses.AsNoTracking().ToList();
            int Code(string c) => (int)(q.FirstOrDefault(s => s.Code == c)?.StatusId ?? -1);
            var p = Code(ReservationStatusCodes.Pending); var a = Code(ReservationStatusCodes.Approved); var r = Code(ReservationStatusCodes.Rejected);
            var c = Code(ReservationStatusCodes.Cancelled); var comp = Code(ReservationStatusCodes.Completed); var e = Code(ReservationStatusCodes.Expired);
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
            var active = Code(BorrowingStatusCodes.Active); var partial = Code(BorrowingStatusCodes.PartiallyReturned);
            var returned = Code(BorrowingStatusCodes.Returned); var overdue = Code(BorrowingStatusCodes.Overdue); var lost = Code(BorrowingStatusCodes.Lost);
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

        public int CountUsers()
        {
            return this.Context.Users.Count();
        }

        public int SumReservedUnits()
        {
            return this.Context.EquipmentInventoryBalances.Sum(b => (int?)b.ReservedQuantity) ?? 0;
        }

        public int SumReleasedUnits()
        {
            return this.Context.EquipmentInventoryBalances.Sum(b => (int?)b.ReleasedQuantity) ?? 0;
        }

        public int CountReservationsWithStatus(string statusCode)
        {
            return this.Context.Reservations.Count(r => r.Status.Code == statusCode);
        }

        public int CountEquipmentInConditionsOrWithMaintenanceOrUnreconciled(IReadOnlyCollection<string> conditionCodes)
        {
            return this.Context.Equipment
                .Count(e => conditionCodes.Contains(e.CurrentCondition.Code)
                    || (e.EquipmentInventoryBalance != null
                        && (e.EquipmentInventoryBalance.MaintenanceQuantity > 0 || e.EquipmentInventoryBalance.UnreconciledQuantity > 0)));
        }

        public List<TransactionRow> GetReservationTransactions()
        {
            return this.Context.Reservations
                .Include(r => r.BorrowerUser)
                .Include(r => r.Equipment)
                .Include(r => r.Status)
                .AsNoTracking()
                .Select(r => new TransactionRow
                {
                    Type = "Reservation",
                    Reference = r.ReservationNumber,
                    Date = r.SubmittedAtUtc,
                    Actor = r.BorrowerUser.FirstName + " " + r.BorrowerUser.LastName,
                    Equipment = r.Equipment.EquipmentName,
                    AssetCode = r.Equipment.AssetCode,
                    Quantity = r.RequestedQuantity,
                    Status = r.Status.Description,
                    StatusCode = r.Status.Code,
                })
                .ToList();
        }

        public List<TransactionRow> GetBorrowingTransactions()
        {
            return this.Context.Borrowings
                .Include(b => b.BorrowerUser)
                .Include(b => b.Equipment)
                .Include(b => b.Status)
                .AsNoTracking()
                .Select(b => new TransactionRow
                {
                    Type = "Borrowing",
                    Reference = b.TransactionNumber,
                    Date = b.ReleasedAtUtc,
                    Actor = b.BorrowerUser.FirstName + " " + b.BorrowerUser.LastName,
                    Equipment = b.Equipment.EquipmentName,
                    AssetCode = b.Equipment.AssetCode,
                    Quantity = b.ReleasedQuantity,
                    Status = b.Status.Description,
                    StatusCode = b.Status.Code,
                    DueAtUtc = b.DueAtUtc,
                })
                .ToList();
        }

        public List<TransactionRow> GetReturnTransactions(string returnedStatusCode)
        {
            return this.Context.BorrowingReturns
                .AsNoTracking()
                .Select(r => new TransactionRow
                {
                    Type = "Return",
                    Reference = r.Borrowing.TransactionNumber,
                    Date = r.ReturnedAtUtc,
                    Actor = r.Borrowing.BorrowerUser.FirstName + " " + r.Borrowing.BorrowerUser.LastName,
                    Equipment = r.Borrowing.Equipment.EquipmentName,
                    AssetCode = r.Borrowing.Equipment.AssetCode,
                    Quantity = r.ReturnedQuantity,
                    Status = r.WasReturnedLate ? "Returned Late" : "Returned",
                    StatusCode = returnedStatusCode,
                })
                .ToList();
        }

        public List<TransactionRow> GetInspectionTransactions()
        {
            return this.Context.ConditionInspections
                .AsNoTracking()
                .Select(i => new TransactionRow
                {
                    Type = "Inspection",
                    Reference = i.Borrowing.TransactionNumber,
                    Date = i.CreatedAtUtc,
                    Actor = i.InspectedByUser.FirstName + " " + i.InspectedByUser.LastName,
                    Equipment = i.Borrowing.Equipment.EquipmentName,
                    AssetCode = i.Borrowing.Equipment.AssetCode,
                    Quantity = i.InspectedQuantity,
                    Status = i.InspectionStatus.Description,
                    StatusCode = i.InspectionStatus.Code,
                })
                .ToList();
        }

        public List<ReservationExportRow> GetReservationExportRows(DateTime? fromUtc)
        {
            return this.Context.Reservations.AsNoTracking()
                .Where(r => fromUtc == null || r.SubmittedAtUtc >= fromUtc)
                .OrderByDescending(r => r.SubmittedAtUtc)
                .Select(r => new ReservationExportRow
                {
                    ReservationNumber = r.ReservationNumber,
                    SubmittedAtUtc = r.SubmittedAtUtc,
                    Borrower = r.BorrowerUser.FirstName + " " + r.BorrowerUser.LastName,
                    BorrowerId = r.BorrowerUser.AccountIdentifier,
                    EquipmentName = r.Equipment.EquipmentName,
                    AssetCode = r.Equipment.AssetCode,
                    RequestedQuantity = r.RequestedQuantity,
                    ApprovedQuantity = r.ApprovedQuantity,
                    Status = r.Status.Code,
                    Custodian = r.AssignedCustodianUser == null ? null : r.AssignedCustodianUser.FirstName + " " + r.AssignedCustodianUser.LastName,
                    RequestedStartAtUtc = r.RequestedStartAtUtc,
                    ExpectedReturnAtUtc = r.ExpectedReturnAtUtc,
                    CompletedAtUtc = r.CompletedAtUtc,
                })
                .ToList();
        }

        public List<BorrowingExportRow> GetBorrowingExportRows(DateTime? fromUtc)
        {
            return this.Context.Borrowings.AsNoTracking()
                .Where(b => fromUtc == null || b.ReleasedAtUtc >= fromUtc)
                .OrderByDescending(b => b.ReleasedAtUtc)
                .Select(b => new BorrowingExportRow
                {
                    TransactionNumber = b.TransactionNumber,
                    Reservation = b.Reservation == null ? null : b.Reservation.ReservationNumber,
                    Borrower = b.BorrowerUser.FirstName + " " + b.BorrowerUser.LastName,
                    BorrowerId = b.BorrowerUser.AccountIdentifier,
                    EquipmentName = b.Equipment.EquipmentName,
                    AssetCode = b.Equipment.AssetCode,
                    ReleasedQuantity = b.ReleasedQuantity,
                    Returned = b.BorrowingReturns.Sum(x => (int?)x.ReturnedQuantity) ?? 0,
                    ReleasedAtUtc = b.ReleasedAtUtc,
                    DueAtUtc = b.DueAtUtc,
                    Status = b.Status.Code,
                    Inspection = b.InspectionStatus.Code,
                    ReleasedBy = b.ReleasedByUser.FirstName + " " + b.ReleasedByUser.LastName,
                })
                .ToList();
        }
    }
}
