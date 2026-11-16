using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using ASI.Basecode.Data.QueryModels;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Repository for the borrower reservation flow (Figma B screens) and
    /// the equipment-availability query that backs the reservation form.
    /// Queries only — business rules live in the service layer.
    /// </summary>
    public interface IReservationRepository
    {
        /// <summary>
        /// Borrower-portal equipment availability listing: ACTIVE equipment with
        /// category, location, condition, operational status and inventory balance.
        /// </summary>
        PagedResult<Equipment> GetAvailableEquipmentPaged(string search, long? categoryId, int page, int pageSize);

        /// <summary>
        /// Single ACTIVE equipment record with full navigations for the reservation form summary panel.
        /// Returns null when the record does not exist or is inactive.
        /// </summary>
        Equipment GetReservableEquipment(long equipmentId);

        /// <summary>
        /// Single reservation with equipment, borrower (+role), pickup location, status and policy navigations.
        /// Not tracked (read scenarios).
        /// </summary>
        Reservation GetByIdWithDetails(long reservationId);

        /// <summary>
        /// Single reservation (tracked) for update scenarios such as cancel.
        /// </summary>
        Reservation GetTrackedById(long reservationId);

        /// <summary>
        /// All reservations of a borrower with detail navigations, newest first.
        /// Status filtering is left to the service (status codes live in the DB).
        /// </summary>
        List<Reservation> GetForBorrower(long borrowerUserId);

        /// <summary>
        /// True when a reservation assigned to the custodian is in one of the given statuses.
        /// </summary>
        bool AnyAssignedToCustodianInStatuses(long custodianUserId, IReadOnlyCollection<string> statusCodes);
    }
}
