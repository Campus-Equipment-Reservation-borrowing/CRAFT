using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = RoleCodes.Admin)]
    public class MonitoringController : ControllerBase<MonitoringController>
    {
        private readonly IMonitoringService _service;
        private readonly ICustodianAssignmentService _assignments;
        private readonly ICommunicationService _communication;

        public MonitoringController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IMonitoringService service,
            ICustodianAssignmentService assignments,
            ICommunicationService communication)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._service = service;
            this._assignments = assignments;
            this._communication = communication;
        }

        public IActionResult Dashboard()
        {
            this.ViewBag.Workload = this._assignments.GetWorkload();
            this.ViewBag.VerificationStats = this._communication.GetVerificationStats();
            return this.View(this._service.GetDashboard());
        }

        public IActionResult Transactions(string search, string tab, int page = 1, int pageSize = 15)
        {
            return this.View(this._service.GetTransactions(search, tab, page, pageSize));
        }

        /// <summary>
        /// Tracking &amp; Reports (4.5): operational totals and CSV exports.
        /// </summary>
        public IActionResult Reports()
        {
            return this.View(this._service.GetDashboard());
        }

        public IActionResult AuditLogs(string search, string entityType, int page = 1, int pageSize = 20)
        {
            return this.View(this._service.GetAuditLogs(search, entityType, page, pageSize));
        }

        public IActionResult ExportAuditCsv(string search, string entityType)
        {
            var csv = this._service.ExportAuditLogsCsv(search, entityType);
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return this.File(bytes, "text/csv", "audit-logs.csv");
        }

        public IActionResult ExportMovementsCsv(string search)
        {
            var csv = this._service.ExportMovementsCsv(search);
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return this.File(bytes, "text/csv", "inventory-movements.csv");
        }

        public IActionResult ExportReservationsCsv(System.DateTime? from)
        {
            var csv = this._service.ExportReservationsCsv(from?.ToUniversalTime());
            return this.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "reservations.csv");
        }

        public IActionResult ExportBorrowingsCsv(System.DateTime? from)
        {
            var csv = this._service.ExportBorrowingsCsv(from?.ToUniversalTime());
            return this.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "borrowings.csv");
        }
    }
}
