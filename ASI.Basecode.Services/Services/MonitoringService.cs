using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ViewModelClasses;
using AutoMapper;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ASI.Basecode.Data.QueryModels;
using ASI.Basecode.Resources.Constants;
using ErrorMessages = ASI.Basecode.Resources.Messages.Errors;

namespace ASI.Basecode.Services.Services
{
    public class MonitoringService : ServiceBase, IMonitoringService
    {
        private static readonly string[] DamagedOrLostConditionCodes = { ConditionCodes.Damaged, ConditionCodes.MissingParts, ConditionCodes.Lost };

        private readonly ISettingsRepository _settings;
        private readonly IMonitoringRepository _repository;
        private readonly IMapper _mapper;

        public MonitoringService(
            ILoggerFactory loggerFactory,
            ISettingsRepository settings,
            IMonitoringRepository repository,
            IMapper mapper)
            : base(loggerFactory)
        {
            this._settings = settings;
            this._repository = repository;
            this._mapper = mapper;
        }

        public DashboardViewModel GetDashboard()
        {
            var (resTotal, resPending, resApproved, resRejected, resCancelled, resCompleted, resExpired) = this._repository.GetReservationCounts();
            var (borTotal, borActive, borPartial, borReturned, borOverdue, borLost) = this._repository.GetBorrowingCounts(
                BorrowingRules.GetOverdueCutoffUtc(this._settings, DateTime.UtcNow));
            var (eqTotal, eqUnits, eqAvailable) = this._repository.GetEquipmentCounts();

            var totalUsers = this._repository.CountUsers();
            var reservedUnits = this._repository.SumReservedUnits();
            var borrowedUnits = this._repository.SumReleasedUnits();
            var readyForRelease = this._repository.CountReservationsWithStatus(ReservationStatusCodes.ReadyForRelease);
            // Items flagged damaged/lost, or with units sent to maintenance / recorded lost at inspection.
            var damagedLost = this._repository.CountEquipmentInConditionsOrWithMaintenanceOrUnreconciled(DamagedOrLostConditionCodes);

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
            // Each source is filtered in SQL by tab, then merged in memory (EF can't UNION these projections).
            var wantAll = string.IsNullOrEmpty(tab) || tab == "All";
            var combined = new List<TransactionRowViewModel>();
            if (wantAll || tab == "Reservations") combined.AddRange(this._mapper.Map<List<TransactionRowViewModel>>(this._repository.GetReservationTransactions()));
            if (wantAll || tab == "Borrowings" || tab == "Overdue") combined.AddRange(this._mapper.Map<List<TransactionRowViewModel>>(this._repository.GetBorrowingTransactions()));
            if (wantAll || tab == "Returns") combined.AddRange(this._mapper.Map<List<TransactionRowViewModel>>(this._repository.GetReturnTransactions(BorrowingStatusCodes.Returned)));
            if (wantAll || tab == "Inspections") combined.AddRange(this._mapper.Map<List<TransactionRowViewModel>>(this._repository.GetInspectionTransactions()));

            // Show borrowings that are overdue under the shared rule as "Overdue", even before the sweep runs.
            var cutoff = BorrowingRules.GetOverdueCutoffUtc(this._settings, DateTime.UtcNow);
            foreach (var row in combined.Where(t => t.Type == "Borrowing" && t.DueAtUtc.HasValue
                && BorrowingRules.IsOverdue(t.StatusCode, t.DueAtUtc.Value, cutoff)))
            {
                row.Status = "Overdue";
                row.StatusCode = BorrowingStatusCodes.Overdue;
            }

            IEnumerable<TransactionRowViewModel> filtered = combined;
            if (tab == "Overdue")
            {
                filtered = filtered.Where(t => t.StatusCode == BorrowingStatusCodes.Overdue);
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
            var rows = this._repository.GetReservationExportRows(fromUtc);

            var csv = new Csv("ReservationNumber", "SubmittedAtUtc", "Borrower", "BorrowerId", "Equipment", "AssetCode", "Requested", "Approved", "Status", "Custodian", "StartUtc", "ExpectedReturnUtc", "CompletedAtUtc");
            foreach (var r in rows)
            {
                csv.Row(r.ReservationNumber, r.SubmittedAtUtc, r.Borrower, r.BorrowerId, r.EquipmentName, r.AssetCode, r.RequestedQuantity, r.ApprovedQuantity, r.Status, r.Custodian, r.RequestedStartAtUtc, r.ExpectedReturnAtUtc, r.CompletedAtUtc);
            }
            return csv.ToString();
        }

        public string ExportBorrowingsCsv(DateTime? fromUtc)
        {
            var rows = this._repository.GetBorrowingExportRows(fromUtc);

            var csv = new Csv("TransactionNumber", "ReservationNumber", "Borrower", "BorrowerId", "Equipment", "AssetCode", "Released", "Returned", "ReleasedAtUtc", "DueAtUtc", "Status", "Inspection", "ReleasedBy");
            foreach (var b in rows)
            {
                csv.Row(b.TransactionNumber, b.Reservation, b.Borrower, b.BorrowerId, b.EquipmentName, b.AssetCode, b.ReleasedQuantity, b.Returned, b.ReleasedAtUtc, b.DueAtUtc, b.Status, b.Inspection, b.ReleasedBy);
            }
            return csv.ToString();
        }
    }
}
