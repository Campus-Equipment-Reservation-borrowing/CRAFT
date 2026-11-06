namespace ASI.Basecode.Data
{
    /// <summary>
    /// Aggregate account counters across all users (User Management KPI cards).
    /// </summary>
    public class UserTotals
    {
        public int TotalAccounts { get; set; }

        public int Students { get; set; }

        public int Faculty { get; set; }

        public int CustodianAdmins { get; set; }

        /// <summary>Accounts waiting in the verification queue (UNVERIFIED).</summary>
        public int Unverified { get; set; }
    }
}
