using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.QueryModels
{
    /// <summary>
    /// A user about to receive a notification: id, email and their preference row (null when missing).
    /// </summary>
    public class NotificationRecipient
    {
        public long UserId { get; set; }

        public string SchoolEmail { get; set; }

        public UserPreference Preference { get; set; }
    }
}
