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
using ASI.Basecode.Services.ServiceModels;
using ASI.Basecode.Services.Manager;

namespace ASI.Basecode.Services.Services
{
    /// <inheritdoc cref="IReservationLifecycleService" />
    public class ReservationLifecycleService : ServiceBase, IReservationLifecycleService
    {
        private static readonly string[] OpenBorrowingStatuses = { BorrowingStatusCodes.Active, BorrowingStatusCodes.PartiallyReturned };

        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditLogRepository _auditLogs;
        private readonly INotificationDispatcher _notifications;
        private readonly ReservationLifecycleOptions _options;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public ReservationLifecycleService(
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            INotificationDispatcher notifications,
            IOptions<ReservationLifecycleOptions> options,
            IAuditLogRepository auditLogs)
            : base(loggerFactory)
        {
            this._auditLogs = auditLogs;
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
                ExpiryWarnings = this.WarnExpiringReservations(now),
                MarkedOverdue = this.MarkOverdueBorrowings(now),
                DueReminders = this.SendDueReminders(now),
            };
        }

        private int ReviewHours => Math.Max(1, this._options.AutoCancelHours);

        /// <summary>
        /// PENDING reservations not reviewed by their deadline (submission + review window, capped
        /// at the requested pickup) and APPROVED ones never picked up within the window after
        /// pickup start become EXPIRED and give their held units back.
        /// </summary>
        private int ExpireStaleReservations(DateTime now)
        {
            var expired = this.DbContext.ReservationStatuses.FirstOrDefault(s => s.Code == ReservationStatusCodes.Expired);
            if (expired == null)
            {
                return 0;
            }

            var submittedBefore = now.AddHours(-this.ReviewHours);
            var pickupBefore = now.AddHours(-this.ReviewHours);
            var stale = this.DbContext.Reservations
                .Include(r => r.Status)
                .Include(r => r.Equipment).ThenInclude(e => e.EquipmentInventoryBalance)
                .Where(r => (r.Status.Code == ReservationRules.Pending && (r.SubmittedAtUtc < submittedBefore || r.RequestedStartAtUtc < now))
                    || (r.Status.Code == ReservationRules.Approved && r.RequestedStartAtUtc < pickupBefore))
                .ToList();
            if (stale.Count == 0)
            {
                return 0;
            }

            var expiredRows = new List<(Reservation Reservation, string PreviousStatus, int Released)>();
            foreach (var reservation in stale)
            {
                var previousStatus = reservation.Status.Code;
                var hold = ReservationRules.RemainingHold(reservation, previousStatus, ReservationRules.ReleasedSoFar(this.DbContext, reservation.ReservationId));
                var balance = reservation.Equipment?.EquipmentInventoryBalance;
                var released = 0;
                if (balance != null && hold > 0)
                {
                    released = Math.Min(hold, balance.ReservedQuantity);
                    balance.ReservedQuantity -= released;
                }

                reservation.StatusId = expired.StatusId;
                reservation.HeldQuantity = 0;
                reservation.CompletedAtUtc = now;

                this._auditLogs.Add(new AuditLog
                {
                    ActorUserId = null,
                    ActionType = "EXPIRE",
                    EntityType = "Reservation",
                    EntityId = reservation.ReservationId,
                    ReferenceNumber = reservation.ReservationNumber,
                    Details = previousStatus == ReservationRules.Pending
                        ? $"Reservation {reservation.ReservationNumber} expired without a decision; {released} held unit(s) released."
                        : $"Approved reservation {reservation.ReservationNumber} was never picked up; {released} held unit(s) released.",
                    OccurredAtUtc = now,
                });
                expiredRows.Add((reservation, previousStatus, released));
            }

            this._unitOfWork.SaveChanges();

            List<long> allCustodians = null;
            foreach (var (reservation, previousStatus, _) in expiredRows)
            {
                var item = reservation.Equipment?.EquipmentName ?? "equipment";
                this._notifications.Notify(reservation.BorrowerUserId, NotificationTypes.Reservation,
                    "Reservation expired",
                    previousStatus == ReservationRules.Pending
                        ? $"Your reservation {reservation.ReservationNumber} for {item} expired before it was reviewed. The units have been released; you can submit a new request."
                        : $"Your approved reservation {reservation.ReservationNumber} for {item} expired because it wasn't picked up. Submit a new request if you still need it.",
                    $"/Reservations/Details/{reservation.ReservationId}");

                var staff = reservation.AssignedCustodianUserId is long custodianId
                    ? new List<long> { custodianId }
                    : (allCustodians ??= this._notifications.GetActiveUserIdsInRoles(RoleCodes.Technician));
                this._notifications.Notify(staff, NotificationTypes.Reservation,
                    $"Expired: {reservation.ReservationNumber}",
                    previousStatus == ReservationRules.Pending
                        ? $"Reservation {reservation.ReservationNumber} for {item} expired without being reviewed. Its units were released."
                        : $"Approved reservation {reservation.ReservationNumber} for {item} was never picked up and has expired. Its units were released.");
            }

            return stale.Count;
        }

        /// <summary>
        /// Warns the responsible custodian, once, when a PENDING request will expire within
        /// <see cref="ReservationLifecycleOptions.ExpiryWarningHours"/>.
        /// </summary>
        private int WarnExpiringReservations(DateTime now)
        {
            var warnHours = Math.Max(1, this._options.ExpiryWarningHours);
            var candidates = this.DbContext.Reservations
                .Include(r => r.Equipment)
                .Include(r => r.BorrowerUser)
                .Where(r => r.Status.Code == ReservationRules.Pending && r.ExpiryWarningSentAtUtc == null)
                .ToList();

            var expiring = candidates
                .Select(r => new { Reservation = r, Deadline = ReservationRules.ReviewDeadline(r.SubmittedAtUtc, r.RequestedStartAtUtc, this.ReviewHours) })
                .Where(x => x.Deadline > now && x.Deadline <= now.AddHours(warnHours))
                .ToList();
            if (expiring.Count == 0)
            {
                return 0;
            }

            foreach (var x in expiring)
            {
                x.Reservation.ExpiryWarningSentAtUtc = now;
            }

            this._unitOfWork.SaveChanges();

            List<long> allCustodians = null;
            foreach (var x in expiring)
            {
                var r = x.Reservation;
                var staff = r.AssignedCustodianUserId is long custodianId
                    ? new List<long> { custodianId }
                    : (allCustodians ??= this._notifications.GetActiveUserIdsInRoles(RoleCodes.Technician));
                var borrower = r.BorrowerUser == null ? "a borrower" : $"{r.BorrowerUser.FirstName} {r.BorrowerUser.LastName}";
                this._notifications.Notify(staff, NotificationTypes.Reservation,
                    $"Expiring soon: {r.ReservationNumber}",
                    $"Reservation {r.ReservationNumber} for {r.Equipment?.EquipmentName ?? "equipment"} ({borrower}) expires {x.Deadline:dd MMM yyyy, hh:mm tt} UTC if it isn't reviewed. Approve or reject it before then.");
            }

            return expiring.Count;
        }

        /// <summary>Persist OVERDUE once due + grace has passed, and alert borrower, custodian and admins once.</summary>
        private int MarkOverdueBorrowings(DateTime now)
        {
            var overdueStatus = this.DbContext.BorrowingStatuses.FirstOrDefault(s => s.Code == BorrowingStatusCodes.Overdue);
            if (overdueStatus == null)
            {
                return 0;
            }

            var dueBefore = Manager.BorrowingRules.GetOverdueCutoffUtc(this.DbContext, now);

            var late = this.DbContext.Borrowings
                .Include(b => b.Equipment)
                .Include(b => b.Reservation)
                .Where(b => OpenBorrowingStatuses.Contains(b.Status.Code) && b.DueAtUtc < dueBefore)
                .ToList();
            var unnotifiedOverdue = this.DbContext.Borrowings
                .Include(b => b.Equipment)
                .Include(b => b.Reservation)
                .Where(b => b.Status.Code == BorrowingStatusCodes.Overdue && b.OverdueNotifiedAtUtc == null)
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
                    $"{item} under {borrowing.TransactionNumber} was due {borrowing.DueAtUtc:dd MMM yyyy, hh:mm tt} UTC. Please return it as soon as possible; new reservations are blocked while it is overdue.",
                    $"/Borrowings/Details/{borrowing.BorrowingId}");

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
                    $"{item} ({borrowing.TransactionNumber}) is overdue since {borrowing.DueAtUtc:dd MMM yyyy}. Follow up with the borrower.",
                    "/Custodian/Borrowings?search=" + borrowing.TransactionNumber);
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
                    $"{borrowing.Equipment?.EquipmentName ?? "Your borrowed equipment"} ({borrowing.TransactionNumber}) is due back by {borrowing.DueAtUtc:dd MMM yyyy, hh:mm tt} UTC.",
                    $"/Borrowings/Details/{borrowing.BorrowingId}");
            }

            return dueSoon.Count;
        }
    }
}
