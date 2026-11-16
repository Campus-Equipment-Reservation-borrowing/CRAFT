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

        /// <summary>Tracked reservations that are pending past their review window (or pickup) or approved past the pickup window, with status and equipment balance.</summary>
        List<Reservation> GetTrackedStale(string pendingStatusCode, string approvedStatusCode, DateTime submittedBefore, DateTime pickupBefore, DateTime nowUtc);

        /// <summary>Tracked reservations in the status that have not had an expiry warning, with equipment and borrower.</summary>
        List<Reservation> GetTrackedWithoutExpiryWarning(string statusCode);

        /// <summary>Untracked reservations with borrower, equipment and status, limited to the custodian's routed work plus the unassigned pool (all when scope is null).</summary>
        List<Reservation> GetInCustodianScope(long? scopeCustodianUserId);

        /// <summary>Reservations of the equipment in one of the given statuses.</summary>
        int CountForEquipmentInStatuses(long equipmentId, IReadOnlyCollection<string> statusCodes);
    }
}
