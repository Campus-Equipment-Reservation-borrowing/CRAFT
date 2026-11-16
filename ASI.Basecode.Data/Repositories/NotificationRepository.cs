using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    /// <summary>
    /// Notification repository implementation. Data access only.
    /// </summary>
    public class NotificationRepository : BaseRepository, INotificationRepository
    {
        public NotificationRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public void Add(Notification notification)
        {
            this.Context.Notifications.Add(notification);
        }

        public List<Notification> GetLatestForRecipient(long recipientUserId, int take)
        {
            return this.Context.Notifications
                .AsNoTracking()
                .Where(n => n.RecipientUserId == recipientUserId)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(take)
                .Select(n => new Notification
                {
                    NotificationId = n.NotificationId,
                    NotificationType = n.NotificationType,
                    Title = n.Title,
                    Message = n.Message,
                    LinkUrl = n.LinkUrl,
                    IsRead = n.IsRead,
                    CreatedAtUtc = n.CreatedAtUtc,
                })
                .ToList();
        }

        public Notification GetTrackedForRecipient(long notificationId, long recipientUserId)
        {
            return this.Context.Notifications.FirstOrDefault(n => n.NotificationId == notificationId && n.RecipientUserId == recipientUserId);
        }

        public List<Notification> GetTrackedUnreadForRecipient(long recipientUserId)
        {
            return this.Context.Notifications.Where(n => n.RecipientUserId == recipientUserId && !n.IsRead).ToList();
        }

        public int CountUnread(long recipientUserId)
        {
            return this.Context.Notifications.Count(n => n.RecipientUserId == recipientUserId && !n.IsRead);
        }

        public void RemoveAllForRecipient(long recipientUserId)
        {
            this.Context.Notifications.RemoveRange(this.Context.Notifications.Where(n => n.RecipientUserId == recipientUserId));
        }
    }
}
