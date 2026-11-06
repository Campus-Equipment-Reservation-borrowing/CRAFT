using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Single entry point for in-app notifications across every role. Applies each recipient's
    /// <c>user_preferences</c> and hands the message to <see cref="IEmailSender"/>.
    /// </summary>
    public interface INotificationDispatcher
    {
        /// <summary>
        /// Queues one notification per distinct recipient (skipping recipients who opted out of
        /// the type's preference) and saves. Returns how many rows were written.
        /// </summary>
        int Notify(IEnumerable<long> recipientUserIds, string notificationType, string title, string message);

        /// <summary>Convenience overload for a single recipient.</summary>
        int Notify(long recipientUserId, string notificationType, string title, string message);

        /// <summary>Active accounts holding any of the given role codes.</summary>
        List<long> GetActiveUserIdsInRoles(params string[] roleCodes);

        /// <summary>Unread notifications for the header badge.</summary>
        int GetUnreadCount(long userId);
    }
}
