using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Data access for audit rows. Callers decide what to log; this only stores and reads.
    /// </summary>
    public interface IAuditLogRepository
    {
        /// <summary>
        /// Adds an audit row to the context (saved by the caller's SaveChanges).
        /// </summary>
        void Add(AuditLog log);

        /// <summary>
        /// Number of audit rows with the given action type.
        /// </summary>
        int CountByActionType(string actionType);

        /// <summary>
        /// Clears the actor on every audit row written by the user (tracked; saved by the caller).
        /// </summary>
        void ClearActor(long userId);

        /// <summary>
        /// For reservation audit rows of the given action types since a moment, the audit time paired with the
        /// reservation's submission time.
        /// </summary>
        List<(DateTime OccurredAtUtc, DateTime SubmittedAtUtc)> GetReservationDecisionTimes(
            string entityType, IReadOnlyCollection<string> actionTypes, DateTime sinceUtc);
    }
}
