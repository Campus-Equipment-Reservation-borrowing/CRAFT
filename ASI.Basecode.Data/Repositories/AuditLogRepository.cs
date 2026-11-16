using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    /// <summary>
    /// Audit log repository implementation. Data access only.
    /// </summary>
    public class AuditLogRepository : BaseRepository, IAuditLogRepository
    {
        public AuditLogRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public void Add(AuditLog log)
        {
            this.Context.AuditLogs.Add(log);
        }

        public int CountByActionType(string actionType)
        {
            return this.Context.AuditLogs.Count(a => a.ActionType == actionType);
        }

        public void ClearActor(long userId)
        {
            foreach (var log in this.Context.AuditLogs.Where(l => l.ActorUserId == userId))
            {
                log.ActorUserId = null;
            }
        }

        public List<(DateTime OccurredAtUtc, DateTime SubmittedAtUtc)> GetReservationDecisionTimes(
            string entityType, IReadOnlyCollection<string> actionTypes, DateTime sinceUtc)
        {
            return this.Context.AuditLogs
                .Where(a => a.EntityType == entityType
                    && actionTypes.Contains(a.ActionType)
                    && a.OccurredAtUtc >= sinceUtc)
                .Join(this.Context.Reservations, a => a.EntityId, r => (long?)r.ReservationId,
                    (a, r) => new { a.OccurredAtUtc, r.SubmittedAtUtc })
                .AsEnumerable()
                .Select(x => (x.OccurredAtUtc, x.SubmittedAtUtc))
                .ToList();
        }
    }
}
