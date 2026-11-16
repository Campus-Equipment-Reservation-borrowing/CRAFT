namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// Codes in the shared <c>conditions</c> codebook, used both for an item's physical condition and
    /// for its operational status.
    /// </summary>
    public static class ConditionCodes
    {
        // Physical condition
        public const string Good = "GOOD";
        public const string MinorDamage = "MINOR_DAMAGE";
        public const string Damaged = "DAMAGED";
        public const string MissingParts = "MISSING_PARTS";
        public const string Lost = "LOST";

        // Operational status
        public const string Available = "AVAILABLE";
        public const string UnderMaintenance = "UNDER_MAINTENANCE";
        public const string Broken = "BROKEN";
        public const string Retired = "RETIRED";
    }
}
