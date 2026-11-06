using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    /// <inheritdoc cref="IReservationLifecycleService" />
    public class ReservationLifecycleService : ServiceBase, IReservationLifecycleService
    {
        private static readonly string[] OpenBorrowingStatuses = { "ACTIVE", "PARTIALLY_RETURNED" };

        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationDispatcher _notifications;
        private readonly ReservationLifecycleOptions _options;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public ReservationLifecycleService(
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            INotificationDispatcher notifications,
            IOptions<ReservationLifecycleOptions> options)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
            this._notifications = notifications;
            this._options = options?.Value ?? new ReservationLifecycleOptions();
        }

        public LifecycleRunResult RunOnce(DateTime? nowUtc = null)
        {
            var now = nowUtc ?? DateTime.UtcNow;
            return new LifecycleRunResult
            {
                Expired = this.ExpireStaleReservations(now),
                MarkedOverdue = this.MarkOverdueBorrowings(now),
                DueReminders = this.SendDueReminders(now),
            };
        }

        /// <summary>
        /// PENDING reservations not acted on within <see cref="ReservationLifecycleOptions.AutoCancelHours"/>,
        /// or whose requested start has already passed, become EXPIRED and give their held units back.
        /// </summary>
        private int ExpireStaleReservations(DateTime now)
        {
            var expired = this.DbContext.ReservationStatuses.FirstOrDefault(s => s.Code == "EXPIRED");
            if (expired == null)
            {
                return 0;
            }

            var cutoff = now.AddHours(-Math.Max(1, this._options.AutoCancelHours));
            var stale = this.DbContext.Reservations
                .Include(r => r.Equipment).ThenInclude(e => e.EquipmentInventoryBalance)
                .Where(r => r.Status.Code == "PENDING" && (r.SubmittedAtUtc < cutoff || r.RequestedStartAtUtc < now))
                .ToList();

            foreach (var reservation in stale)
            {
                var balance = reservation.Equipment?.EquipmentInventoryBalance;
                if (balance != null)
                {
                    balance.ReservedQuantity = Math.Max(0, balance.ReservedQuantity - reservation.RequestedQuantity);
                }

                reservation.StatusId = expired.StatusId;
                reservation.HeldQuantity = 0;
                reservation.CompletedAtUtc = now;

                this.DbContext.AuditLogs.Add(new AuditLog
                {
                    ActorUserId = null,
                    ActionType = "EXPIRE",
                    EntityType = "Reservation",
                    EntityId = reservation.ReservationId,
                    ReferenceNumber = reservation.ReservationNumber,
                    Details = $"Reservation {reservation.ReservationNumber} expired without a decision; {reservation.RequestedQuantity} held unit(s) released.",
                    OccurredAtUtc = now,
                });
            }

            if (stale.Count == 0)
            {
                return 0;
            }

            this._unitOfWork.SaveChanges();

            foreach (var reservation in stale)
            {
                this._notifications.Notify(reservation.BorrowerUserId, NotificationTypes.Reservation,
                    "Reservation expired",
                    $"Your reservation {reservation.ReservationNumber} for {reservation.Equipment?.EquipmentName} expired before it was reviewed. The units have been released; you can submit a new request.");
            }

            return stale.Count;
        }

        /// <summary>Persist OVERDUE once due + grace has passed, and alert borrower, custodian and admins once.</summary>
        private int MarkOverdueBorrowings(DateTime now)
        {
            var overdueStatus = this.DbContext.BorrowingStatuses.FirstOrDefault(s => s.Code == "OVERDUE");
            if (overdueStatus == null)
            {
                return 0;
            }

            var graceMinutes = this.DbContext.BorrowingPolicyVersions.AsNoTracking()
                .Where(p => p.IsCurrent)
                .Select(p => (int?)p.OverdueGracePeriodMinutes)
                .FirstOrDefault() ?? 0;
            var dueBefore = now.AddMinutes(-Math.Max(0, graceMinutes));

            var late = this.DbContext.Borrowings
                .Include(b => b.Equipment)
                .Include(b => b.Reservation)
                .Where(b => OpenBorrowingStatuses.Contains(b.Status.Code) && b.DueAtUtc < dueBefore)
                .ToList();
            var unnotifiedOverdue = this.DbContext.Borrowings
                .Include(b => b.Equipment)
                .Include(b => b.Reservation)
                .Where(b => b.Status.Code == "OVERDUE" && b.OverdueNotifiedAtUtc == null)
                .ToList();

            foreach (var borrowing in late)
            {
                borrowing.StatusId = overdueStatus.StatusId;
            }

            var toNotify = late.Concat(unnotifiedOverdue).Where(b => b.OverdueNotifiedAtUtc == null).DistinctBy(b => b.BorrowingId).ToList();
            foreach (var borrowing in toNotify)
            {
                borrowing.OverdueNotifiedAtUtc = now;
            }

            if (late.Count == 0 && toNotify.Count == 0)
            {
                return 0;
            }

            this._unitOfWork.SaveChanges();

            var admins = this._notifications.GetActiveUserIdsInRoles(RoleCodes.Admin);
            List<long> allCustodians = null;
            foreach (var borrowing in toNotify)
            {
                var item = borrowing.Equipment?.EquipmentName ?? "equipment";
                this._notifications.Notify(borrowing.BorrowerUserId, NotificationTypes.Overdue,
                    "Equipment overdue",
                    $"{item} under {borrowing.TransactionNumber} was due {borrowing.DueAtUtc:dd MMM yyyy, hh:mm tt} UTC. Please return it as soon as possible; new reservations are blocked while it is overdue.");

                var staff = new List<long>(admins);
                if (borrowing.Reservation?.AssignedCustodianUserId is long custodianId)
                {
                    staff.Add(custodianId);
                }
                else
                {
                    allCustodians ??= this._notifications.GetActiveUserIdsInRoles(RoleCodes.Technician);
                    staff.AddRange(allCustodians);
                }

                this._notifications.Notify(staff, NotificationTypes.Overdue,
                    $"Overdue: {borrowing.TransactionNumber}",
                    $"{item} ({borrowing.TransactionNumber}) is overdue since {borrowing.DueAtUtc:dd MMM yyyy}. Follow up with the borrower.");
            }

            return late.Count;
        }

        private int SendDueReminders(DateTime now)
        {
            var windowEnd = now.AddHours(Math.Max(1, this._options.DueReminderHours));
            var dueSoon = this.DbContext.Borrowings
                .Include(b => b.Equipment)
                .Where(b => OpenBorrowingStatuses.Contains(b.Status.Code)
                    && b.DueReminderSentAtUtc == null
                    && b.DueAtUtc >= now
                    && b.DueAtUtc <= windowEnd)
                .ToList();

            if (dueSoon.Count == 0)
            {
                return 0;
            }

            foreach (var borrowing in dueSoon)
            {
                borrowing.DueReminderSentAtUtc = now;
            }

            this._unitOfWork.SaveChanges();

            foreach (var borrowing in dueSoon)
            {
                this._notifications.Notify(borrowing.BorrowerUserId, NotificationTypes.DueReminder,
                    "Return due soon",
                    $"{borrowing.Equipment?.EquipmentName ?? "Your borrowed equipment"} ({borrowing.TransactionNumber}) is due back by {borrowing.DueAtUtc:dd MMM yyyy, hh:mm tt} UTC.");
            }

            return dueSoon.Count;
        }
    }
}
