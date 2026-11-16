namespace ASI.Basecode.Resources.Constants
{
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

        /// <summary>Fingerprint of the password hash; a reset elsewhere ends older sessions.</summary>
        public const string PasswordStamp = "cp:pwd_stamp";
    }
}
