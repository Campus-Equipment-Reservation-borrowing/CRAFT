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
    }
}
