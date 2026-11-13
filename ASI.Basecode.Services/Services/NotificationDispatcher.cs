using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    /// <inheritdoc cref="INotificationDispatcher" />
    public class NotificationDispatcher : ServiceBase, INotificationDispatcher
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public NotificationDispatcher(ILoggerFactory loggerFactory, IUnitOfWork unitOfWork, IEmailSender emailSender)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
            this._emailSender = emailSender;
        }

        public int Notify(long recipientUserId, string notificationType, string title, string message, string linkUrl = null)
        {
            return this.Notify(new[] { recipientUserId }, notificationType, title, message, linkUrl);
        }

        public int Notify(IEnumerable<long> recipientUserIds, string notificationType, string title, string message, string linkUrl = null)
        {
            var ids = recipientUserIds?.Where(id => id > 0).Distinct().ToList() ?? new List<long>();
            if (ids.Count == 0)
            {
                return 0;
            }

            var recipients = this.DbContext.Users
                .AsNoTracking()
                .Include(u => u.Preference)
                .Where(u => ids.Contains(u.UserId))
                .Select(u => new { u.UserId, u.SchoolEmail, u.Preference })
                .ToList();

            var now = DateTime.UtcNow;
            var written = 0;
            foreach (var recipient in recipients)
            {
                if (!Allows(recipient.Preference, notificationType))
                {
                    continue;
                }

                this.DbContext.Notifications.Add(new Notification
                {
                    RecipientUserId = recipient.UserId,
                    NotificationType = notificationType,
                    // Column limits: a long announcement body or quoted note must not fail the caller.
                    Title = Truncate(title, 200),
                    Message = Truncate(message, 1000),
                    LinkUrl = linkUrl,
                    IsRead = false,
                    CreatedAtUtc = now,
                });
                written++;

                try
                {
                    this._emailSender.Send(recipient.SchoolEmail, title, message);
                }
                catch (Exception ex)
                {
                    // Email is best-effort; the in-app notification still goes out.
                    this._logger.LogWarning(ex, "Email send failed for user {UserId}.", recipient.UserId);
                }
            }

            if (written > 0)
            {
                this._unitOfWork.SaveChanges();
            }

            return written;
        }

        public List<long> GetActiveUserIdsInRoles(params string[] roleCodes)
        {
            if (roleCodes == null || roleCodes.Length == 0)
            {
                return new List<long>();
            }

            return this.DbContext.Users
                .AsNoTracking()
                .Where(u => roleCodes.Contains(u.Role.RoleCode) && u.AccountStatus.Code == AccountStatusCodes.Active)
                .Select(u => u.UserId)
                .ToList();
        }

        public int GetUnreadCount(long userId)
        {
            return this.DbContext.Notifications.Count(n => n.RecipientUserId == userId && !n.IsRead);
        }

        /// <summary>Missing preference rows mean "all on" (the seeder backfills defaults).</summary>
        private static bool Allows(UserPreference preference, string notificationType)
        {
            if (preference == null)
            {
                return true;
            }

            return notificationType switch
            {
                NotificationTypes.Reservation => preference.NotifyReservationUpdates,
                NotificationTypes.DueReminder => preference.NotifyDueReminders,
                NotificationTypes.Overdue => preference.NotifyOverdueAlerts,
                NotificationTypes.Return => preference.NotifyReturnConfirmations,
                NotificationTypes.Announcement => preference.NotifyAnnouncements,
                _ => true,
            };
        }

        private static string Truncate(string value, int max)
            => value != null && value.Length > max ? value.Substring(0, max - 3) + "..." : value;
    }
}
