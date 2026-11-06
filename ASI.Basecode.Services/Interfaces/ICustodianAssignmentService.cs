using ASI.Basecode.Services.ViewModelClasses;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Custodian ownership: who is responsible for which borrowers, locations and equipment
    /// categories, and how reservations are routed and reassigned.
    /// </summary>
    public interface ICustodianAssignmentService
    {
        CustodianAssignmentIndexViewModel GetIndex();

        /// <summary>
        /// Assigns a target (borrower user id / location id / category id, per
        /// <paramref name="scopeType"/>) to an active custodian. Each target has one custodian.
        /// </summary>
        (bool Success, string Error) Assign(string scopeType, long custodianUserId, long targetId);

        (bool Success, string Error) Unassign(long assignmentId);

        /// <summary>
        /// Routing rule for a new reservation: the borrower's custodian, else the custodian for the
        /// equipment's location, else for its category; null means the shared pool.
        /// </summary>
        long? ResolveCustodian(long borrowerUserId, long equipmentId);

        UserAssignmentSummaryViewModel GetUserSummary(long userId);

        CustodianWorkloadViewModel GetWorkload();

        /// <summary>Admin: route an open reservation to a custodian, or back to the pool (null).</summary>
        (bool Success, string Error) Reassign(long reservationId, long? custodianUserId);
    }
}
