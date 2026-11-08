using ASI.Basecode.Services.ViewModelClasses;
using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    public interface IMonitoringService
    {
        DashboardViewModel GetDashboard();

        TransactionMonitoringViewModel GetTransactions(string search, string tab, int page, int pageSize);

        AuditLogsViewModel GetAuditLogs(string search, DateTime? dateFrom, DateTime? dateTo,
                                string user, string role, string actionType, int page);

        string ExportAuditLogsCsv(string search, DateTime? dateFrom, DateTime? dateTo,
                                  string user, string role, string actionType);

        string ExportMovementsCsv(string search);

        /// <summary>All reservations (optionally since a date) as CSV.</summary>
        string ExportReservationsCsv(System.DateTime? fromUtc);

        /// <summary>All borrowings (optionally since a date) as CSV.</summary>
        string ExportBorrowingsCsv(System.DateTime? fromUtc);
    }
}
