namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// Role codes as stored in <c>roles.role_code</c>.
    /// Used for <c>[Authorize(Roles = ...)]</c> and dashboard routing.
    /// </summary>
    public static class RoleCodes
    {
        public const string Student = "STUDENT";
        public const string Faculty = "FACULTY";

        /// <summary>Custodian in the HLD; stored as TECHNICIAN in the codebook.</summary>
        public const string Technician = "TECHNICIAN";

        public const string Admin = "ADMIN";

        /// <summary>Both borrower roles — convenient for <c>[Authorize(Roles = RoleCodes.Borrower)]</c>.</summary>
        public const string Borrower = Student + "," + Faculty;

        /// <summary>Operational staff: custodian + administrator.</summary>
        public const string Staff = Technician + "," + Admin;
    }

    /// <summary>
    /// Account status codes as stored in <c>account_statuses.code</c>.
    /// </summary>
    public static class AccountStatusCodes
    {
        public const string Active = "ACTIVE";

        /// <summary>May sign in, but cannot submit reservations until verified in [4.2].</summary>
        public const string Unverified = "UNVERIFIED";

        public const string Inactive = "INACTIVE";
        public const string Suspended = "SUSPENDED";
        public const string Locked = "LOCKED";

        /// <summary>Verification was declined by an administrator; may sign in to read the reason and appeal.</summary>
        public const string Rejected = "REJECTED";
    }

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

    /// <summary>
    /// <c>custodian_assignments.scope_type</c> values.
    /// </summary>
    public static class CustodianScopeTypes
    {
        public const string Borrower = "BORROWER";
        public const string Location = "LOCATION";
        public const string Category = "CATEGORY";
    }

    /// <summary><c>support_tickets.category</c> values.</summary>
    public static class SupportTicketCategories
    {
        public const string Verification = "VERIFICATION";
        public const string Appeal = "APPEAL";
        public const string Feedback = "FEEDBACK";
        public const string Other = "OTHER";

        public static readonly string[] All = { Verification, Appeal, Feedback, Other };
    }

    /// <summary><c>support_tickets.status</c> values.</summary>
    public static class SupportTicketStatuses
    {
        public const string Open = "OPEN";
        public const string Resolved = "RESOLVED";
    }

    /// <summary>
    /// Extra claim types issued alongside the standard ones.
    /// </summary>
    public static class CampusPulseClaims
    {
        public const string AccountStatus = "cp:account_status";
        public const string AccountIdentifier = "cp:account_identifier";
        public const string IsBorrower = "cp:is_borrower";
        public const string Department = "cp:department";

        /// <summary>"1" while the account still uses an admin-issued temporary password.</summary>
        public const string MustChangePassword = "cp:must_change_password";
    }
}
