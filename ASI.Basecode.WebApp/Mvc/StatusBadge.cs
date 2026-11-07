using System;

namespace ASI.Basecode.WebApp.Mvc
{
    /// <summary>
    /// One colour per status code so the same state reads the same on borrower and staff pages.
    /// </summary>
    public static class StatusBadge
    {
        /// <summary>Badge classes for a reservation, borrowing, inspection or ticket status code.</summary>
        public static string Css(string code) => "cp-badge cp-badge-" + Tone(code);

        /// <summary>Badge classes for an account status code (ACTIVE is good news here).</summary>
        public static string AccountCss(string code) => "cp-badge cp-badge-" + (code?.ToUpperInvariant() switch
        {
            "ACTIVE" => "success",
            "UNVERIFIED" => "warning",
            "REJECTED" or "SUSPENDED" or "LOCKED" => "error",
            _ => "neutral",
        });

        public static string Tone(string code) => code?.ToUpperInvariant() switch
        {
            "PENDING" or "PARTIALLY_RETURNED" or "DUE_TODAY" or "OPEN" => "warning",
            "APPROVED" or "READY_FOR_RELEASE" or "PARTIALLY_RELEASED" or "ACTIVE" => "info",
            "COMPLETED" or "RETURNED" or "RESOLVED" or "AVAILABLE" or "GOOD" => "success",
            "REJECTED" or "OVERDUE" or "LOST" or "DAMAGED" or "UNDER_MAINTENANCE" or "OUT_OF_STOCK" => "error",
            "MINOR_DAMAGE" or "MISSING_PARTS" => "warning",
            _ => "neutral",
        };
    }
}
