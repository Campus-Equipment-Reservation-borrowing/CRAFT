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
}
