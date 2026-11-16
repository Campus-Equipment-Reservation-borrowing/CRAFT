using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.QueryModels;
using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IMonitoringRepository
    {
        (int totalReservations, int pending, int approved, int rejected, int cancelled, int completed, int expired) GetReservationCounts();

        (int totalBorrowings, int active, int partiallyReturned, int returned, int overdue, int lost) GetBorrowingCounts(DateTime overdueCutoffUtc);

        (int totalEquipment, int totalUnits, int availableUnits) GetEquipmentCounts();

        PagedResult<AuditLog> GetAuditLogsPaged(string search, string entityType, int page, int pageSize);

        PagedResult<InventoryMovement> GetMovementsPaged(string search, int page, int pageSize);

        int CountUsers();

        /// <summary>Units held by reservations across all balances.</summary>
        int SumReservedUnits();

        /// <summary>Units currently released across all balances.</summary>
        int SumReleasedUnits();

        int CountReservationsWithStatus(string statusCode);

        /// <summary>Equipment whose condition is one of the codes, or with maintenance / unreconciled units.</summary>
        int CountEquipmentInConditionsOrWithMaintenanceOrUnreconciled(IReadOnlyCollection<string> conditionCodes);

        List<TransactionRow> GetReservationTransactions();

        List<TransactionRow> GetBorrowingTransactions();

        /// <summary>Return events; every row carries the given status code.</summary>
        List<TransactionRow> GetReturnTransactions(string returnedStatusCode);

        List<TransactionRow> GetInspectionTransactions();

        /// <summary>Reservations submitted since the date (all when null), newest first.</summary>
        List<ReservationExportRow> GetReservationExportRows(DateTime? fromUtc);

        /// <summary>Borrowings released since the date (all when null), newest first.</summary>
        List<BorrowingExportRow> GetBorrowingExportRows(DateTime? fromUtc);
    }
}
