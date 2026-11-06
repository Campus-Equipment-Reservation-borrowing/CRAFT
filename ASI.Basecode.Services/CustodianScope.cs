using ASI.Basecode.Services.Interfaces;

namespace ASI.Basecode.Services
{
    /// <summary>
    /// Custodian scoping rule shared by the custodian, returns and dashboard services:
    /// a custodian works on reservations routed to them plus the unassigned pool; admins see all.
    /// </summary>
    public static class CustodianScope
    {
        /// <summary>
        /// Scope to pass to repository queries: null for admins (unscoped), the actor's id for
        /// custodians. An unidentified actor gets an id that matches no assignment.
        /// </summary>
        public static long? GetCustodianScope(this ICurrentUserService currentUser)
        {
            return currentUser.IsActorAdmin() ? null : currentUser.GetActorUserId() ?? -1;
        }

        /// <summary>True when the actor may act on work routed to <paramref name="assignedCustodianUserId"/>.</summary>
        public static bool CanActOn(this ICurrentUserService currentUser, long? assignedCustodianUserId)
        {
            return currentUser.IsActorAdmin()
                || assignedCustodianUserId == null
                || assignedCustodianUserId == currentUser.GetActorUserId();
        }
    }
}
