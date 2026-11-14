using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ViewModelClasses;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ErrorMessages = ASI.Basecode.Resources.Messages.Errors;

namespace ASI.Basecode.Services.Services
{
    public class MonitoringService : ServiceBase, IMonitoringService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMonitoringRepository _repository;
        private readonly IMapper _mapper;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public MonitoringService(
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            IMonitoringRepository repository,
            IMapper mapper)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
            this._repository = repository;
            this._mapper = mapper;
        }

        public DashboardViewModel GetDashboard()
        {
            var (resTotal, resPending, resApproved, resRejected, resCancelled, resCompleted, resExpired) = this._repository.GetReservationCounts();
            var (borTotal, borActive, borPartial, borReturned, borOverdue, borLost) = this._repository.GetBorrowingCounts(
                BorrowingRules.GetOverdueCutoffUtc(this.DbContext, DateTime.UtcNow));
            var (eqTotal, eqUnits, eqAvailable) = this._repository.GetEquipmentCounts();

            var totalUsers = this.DbContext.Users.Count();
            var reservedUnits = this.DbContext.EquipmentInventoryBalances.Sum(b => (int?)b.ReservedQuantity) ?? 0;
            var borrowedUnits = this.DbContext.EquipmentInventoryBalances.Sum(b => (int?)b.ReleasedQuantity) ?? 0;
            var readyForRelease = this.DbContext.Reservations
                .Count(r => r.Status.Code == "READY_FOR_RELEASE");
            // Items flagged damaged/lost, or with units sent to maintenance / recorded lost at inspection.
            var damagedLost = this.DbContext.Equipment
                .Count(e => e.CurrentCondition.Code == "DAMAGED"
                    || e.CurrentCondition.Code == "MISSING_PARTS"
                    || e.CurrentCondition.Code == "LOST"
                    || (e.EquipmentInventoryBalance != null
                        && (e.EquipmentInventoryBalance.MaintenanceQuantity > 0 || e.EquipmentInventoryBalance.UnreconciledQuantity > 0)));

            return new DashboardViewModel
            {
                TotalUsers = totalUsers,
                EquipmentRecords = eqTotal,
                TotalUnits = eqUnits,
                AvailableUnits = eqAvailable,
                ReservedUnits = reservedUnits,
                BorrowedUnits = borrowedUnits,
                DamagedLostUnits = damagedLost,
                ReservationReadyForRelease = readyForRelease,
                ReservationTotal = resTotal,
                ReservationPending = resPending,
                ReservationApproved = resApproved,
                ReservationRejected = resRejected,
                ReservationCancelled = resCancelled,
                ReservationCompleted = resCompleted,
                ReservationExpired = resExpired,
                BorrowingTotal = borTotal,
                BorrowingActive = borActive,
                BorrowingPartial = borPartial,
                BorrowingReturned = borReturned,
                BorrowingOverdue = borOverdue,
                BorrowingLost = borLost,
                RecentAudit = this._repository.GetAuditLogsPaged(null, "All", 1, 8).Items
                    .Select(a => new AuditRowViewModel
                    {
                        OccurredAtUtc = a.OccurredAtUtc,
                        ActorName = a.ActorUser != null ? a.ActorUser.FirstName + " " + a.ActorUser.LastName : "System",
                        ActionType = a.ActionType,
                        EntityType = a.EntityType,
                        ReferenceNumber = a.ReferenceNumber,
                        Details = a.Details,
                    }).ToList(),
            };
        }

        public TransactionMonitoringViewModel GetTransactions(string search, string tab, int page, int pageSize)
        {
            // Combined, date-descending list of reservation, release, return and inspection events.
            var reservations = this.DbContext.Reservations
                .Include(r => r.BorrowerUser)
                .Include(r => r.Equipment)
                .Include(r => r.Status)
                .AsNoTracking()
                .Select(r => new TransactionRowViewModel
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
                });

            var borrowings = this.DbContext.Borrowings
                .Include(b => b.BorrowerUser)
                .Include(b => b.Equipment)
                .Include(b => b.Status)
                .AsNoTracking()
                .Select(b => new TransactionRowViewModel
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
                });

            var returns = this.DbContext.BorrowingReturns
                .AsNoTracking()
                .Select(r => new TransactionRowViewModel
                {
                    Type = "Return",
                    Reference = r.Borrowing.TransactionNumber,
                    Date = r.ReturnedAtUtc,
                    Actor = r.Borrowing.BorrowerUser.FirstName + " " + r.Borrowing.BorrowerUser.LastName,
                    Equipment = r.Borrowing.Equipment.EquipmentName,
                    AssetCode = r.Borrowing.Equipment.AssetCode,
                    Quantity = r.ReturnedQuantity,
                    Status = r.WasReturnedLate ? "Returned Late" : "Returned",
                    StatusCode = "RETURNED",
                });

            var inspections = this.DbContext.ConditionInspections
                .AsNoTracking()
                .Select(i => new TransactionRowViewModel
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
                });

            // Each source is filtered in SQL by tab, then merged in memory (EF can't UNION these projections).
            var wantAll = string.IsNullOrEmpty(tab) || tab == "All";
            var combined = new List<TransactionRowViewModel>();
            if (wantAll || tab == "Reservations") combined.AddRange(reservations.ToList());
            if (wantAll || tab == "Borrowings" || tab == "Overdue") combined.AddRange(borrowings.ToList());
            if (wantAll || tab == "Returns") combined.AddRange(returns.ToList());
            if (wantAll || tab == "Inspections") combined.AddRange(inspections.ToList());

            // Show borrowings that are overdue under the shared rule as "Overdue", even before the sweep runs.
            var cutoff = BorrowingRules.GetOverdueCutoffUtc(this.DbContext, DateTime.UtcNow);
            foreach (var row in combined.Where(t => t.Type == "Borrowing" && t.DueAtUtc.HasValue
                && BorrowingRules.IsOverdue(t.StatusCode, t.DueAtUtc.Value, cutoff)))
            {
                row.Status = "Overdue";
                row.StatusCode = "OVERDUE";
            }

            IEnumerable<TransactionRowViewModel> filtered = combined;
            if (tab == "Overdue")
            {
                filtered = filtered.Where(t => t.StatusCode == "OVERDUE");
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                bool Has(string v) => v != null && v.Contains(term, StringComparison.OrdinalIgnoreCase);
                filtered = filtered.Where(t =>
                    Has(t.Reference) || Has(t.Actor) || Has(t.Equipment) || Has(t.AssetCode) || Has(t.Status));
            }

            var ordered = filtered.OrderByDescending(t => t.Date).ToList();

            const int DefaultPage = 1;
            const int DefaultPageSize = 15;
            page = page < 1 ? DefaultPage : page;
            pageSize = pageSize < 1 ? DefaultPageSize : pageSize;

            var paged = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var totalCount = ordered.Count;

            return new TransactionMonitoringViewModel
            {
                Page = new PagedResult<TransactionRowViewModel>
                {
                    Items = paged,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                },
                Search = search,
                Tab = string.IsNullOrWhiteSpace(tab) ? "All" : tab,
            };
        }

        public AuditLogsViewModel GetAuditLogs(string search, string entityType, int page, int pageSize)
        {
            var result = this._repository.GetAuditLogsPaged(search, entityType, page, pageSize);
            return new AuditLogsViewModel
            {
                Page = new PagedResult<AuditRowViewModel>
                {
                    Items = result.Items.Select(a => new AuditRowViewModel
                    {
                        AuditLogId = a.AuditLogId,
                        OccurredAtUtc = a.OccurredAtUtc,
                        ActorName = a.ActorUser != null ? a.ActorUser.FirstName + " " + a.ActorUser.LastName : "System",
                        ActionType = a.ActionType,
                        EntityType = a.EntityType,
                        ReferenceNumber = a.ReferenceNumber,
                        Details = a.Details,
                    }).ToList(),
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount,
                },
                Search = search,
                EntityType = string.IsNullOrWhiteSpace(entityType) ? "All" : entityType,
                EntityTypes = new List<string> { "All", "Equipment", "User", "Reservation", "Borrowing", "ConditionInspection", "AuditLog" },
            };
        }

        public string ExportAuditLogsCsv(string search, string entityType)
        {
            var result = this._repository.GetAuditLogsPaged(search, entityType, 1, 100000);
            var csv = new Csv("OccurredAtUtc", "Actor", "ActionType", "EntityType", "ReferenceNumber", "Details");
            foreach (var a in result.Items)
            {
                csv.Row(a.OccurredAtUtc, $"{a.ActorUser?.FirstName} {a.ActorUser?.LastName}".Trim(), a.ActionType, a.EntityType, a.ReferenceNumber, a.Details);
            }
            return csv.ToString();
        }

        public string ExportMovementsCsv(string search)
        {
            var result = this._repository.GetMovementsPaged(search, 1, 100000);
            var csv = new Csv("OccurredAtUtc", "Equipment", "AssetCode", "MovementType", "FromState", "ToState", "Quantity", "ReferenceNumber", "PerformedBy");
            foreach (var m in result.Items)
            {
                csv.Row(m.OccurredAtUtc, m.Equipment?.EquipmentName, m.Equipment?.AssetCode, m.MovementType, m.FromState, m.ToState, m.Quantity, m.ReferenceNumber, $"{m.PerformedByUser?.FirstName} {m.PerformedByUser?.LastName}".Trim());
            }
            return csv.ToString();
        }

        public string ExportReservationsCsv(DateTime? fromUtc)
        {
            var rows = this.DbContext.Reservations.AsNoTracking()
                .Where(r => fromUtc == null || r.SubmittedAtUtc >= fromUtc)
                .OrderByDescending(r => r.SubmittedAtUtc)
                .Select(r => new
                {
                    r.ReservationNumber,
                    r.SubmittedAtUtc,
                    Borrower = r.BorrowerUser.FirstName + " " + r.BorrowerUser.LastName,
                    BorrowerId = r.BorrowerUser.AccountIdentifier,
                    r.Equipment.EquipmentName,
                    r.Equipment.AssetCode,
                    r.RequestedQuantity,
                    r.ApprovedQuantity,
                    Status = r.Status.Code,
                    Custodian = r.AssignedCustodianUser == null ? null : r.AssignedCustodianUser.FirstName + " " + r.AssignedCustodianUser.LastName,
                    r.RequestedStartAtUtc,
                    r.ExpectedReturnAtUtc,
                    r.CompletedAtUtc,
                })
                .ToList();

            var csv = new Csv("ReservationNumber", "SubmittedAtUtc", "Borrower", "BorrowerId", "Equipment", "AssetCode", "Requested", "Approved", "Status", "Custodian", "StartUtc", "ExpectedReturnUtc", "CompletedAtUtc");
            foreach (var r in rows)
            {
                csv.Row(r.ReservationNumber, r.SubmittedAtUtc, r.Borrower, r.BorrowerId, r.EquipmentName, r.AssetCode, r.RequestedQuantity, r.ApprovedQuantity, r.Status, r.Custodian, r.RequestedStartAtUtc, r.ExpectedReturnAtUtc, r.CompletedAtUtc);
            }
            return csv.ToString();
        }

        public string ExportBorrowingsCsv(DateTime? fromUtc)
        {
            var rows = this.DbContext.Borrowings.AsNoTracking()
                .Where(b => fromUtc == null || b.ReleasedAtUtc >= fromUtc)
                .OrderByDescending(b => b.ReleasedAtUtc)
                .Select(b => new
                {
                    b.TransactionNumber,
                    Reservation = b.Reservation == null ? null : b.Reservation.ReservationNumber,
                    Borrower = b.BorrowerUser.FirstName + " " + b.BorrowerUser.LastName,
                    BorrowerId = b.BorrowerUser.AccountIdentifier,
                    b.Equipment.EquipmentName,
                    b.Equipment.AssetCode,
                    b.ReleasedQuantity,
                    Returned = b.BorrowingReturns.Sum(x => (int?)x.ReturnedQuantity) ?? 0,
                    b.ReleasedAtUtc,
                    b.DueAtUtc,
                    Status = b.Status.Code,
                    Inspection = b.InspectionStatus.Code,
                    ReleasedBy = b.ReleasedByUser.FirstName + " " + b.ReleasedByUser.LastName,
                })
                .ToList();

            var csv = new Csv("TransactionNumber", "ReservationNumber", "Borrower", "BorrowerId", "Equipment", "AssetCode", "Released", "Returned", "ReleasedAtUtc", "DueAtUtc", "Status", "Inspection", "ReleasedBy");
            foreach (var b in rows)
            {
                csv.Row(b.TransactionNumber, b.Reservation, b.Borrower, b.BorrowerId, b.EquipmentName, b.AssetCode, b.ReleasedQuantity, b.Returned, b.ReleasedAtUtc, b.DueAtUtc, b.Status, b.Inspection, b.ReleasedBy);
            }
            return csv.ToString();
        }
    }
}
