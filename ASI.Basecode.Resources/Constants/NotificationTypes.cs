namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// Values stored in <c>notifications.notification_type</c>. Each maps to a
    /// <c>user_preferences</c> flag in the notification dispatcher (or is always delivered).
    /// </summary>
    public static class NotificationTypes
    {
        public const string Reservation = "RESERVATION";
        public const string DueReminder = "DUE_REMINDER";
        public const string Overdue = "OVERDUE";
        public const string Return = "RETURN";
        public const string Announcement = "ANNOUNCEMENT";

        /// <summary>Account lifecycle (verification, password resets) — always delivered.</summary>
        public const string Account = "ACCOUNT";

        /// <summary>Operational alerts for staff (low stock, damage, loss) — always delivered.</summary>
        public const string Alert = "ALERT";

        /// <summary>Support ticket activity — always delivered.</summary>
        public const string Support = "SUPPORT";
    }
}
