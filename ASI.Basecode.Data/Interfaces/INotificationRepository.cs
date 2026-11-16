using ASI.Basecode.Data.Models;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Data access for in-app notifications.
    /// </summary>
    public interface INotificationRepository
    {
        /// <summary>Adds a notification (saved by the caller's SaveChanges).</summary>
        void Add(Notification notification);

        /// <summary>The recipient's newest notifications (untracked), newest first.</summary>
        List<Notification> GetLatestForRecipient(long recipientUserId, int take);

        /// <summary>A notification of the recipient (tracked), or null when it isn't theirs.</summary>
        Notification GetTrackedForRecipient(long notificationId, long recipientUserId);

        /// <summary>The recipient's unread notifications (tracked).</summary>
        List<Notification> GetTrackedUnreadForRecipient(long recipientUserId);

        /// <summary>Number of unread notifications of the recipient.</summary>
        int CountUnread(long recipientUserId);

        /// <summary>Removes every notification of the recipient (saved by the caller).</summary>
        void RemoveAllForRecipient(long recipientUserId);
    }
}
