namespace ASI.Basecode.Resources.Constants
{
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
}
