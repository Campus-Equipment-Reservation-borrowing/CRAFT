using System;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.WebApp.Helpers
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
            AccountStatusCodes.Active => "success",
            "UNVERIFIED" => "warning",
            AccountStatusCodes.Rejected or "SUSPENDED" or "LOCKED" => "error",
            _ => "neutral",
        });

        public static string Tone(string code) => code?.ToUpperInvariant() switch
        {
            ReservationStatusCodes.Pending or BorrowingStatusCodes.PartiallyReturned or "DUE_TODAY" or "OPEN" => "warning",
            ReservationStatusCodes.Approved or ReservationStatusCodes.ReadyForRelease or ReservationStatusCodes.PartiallyReleased or BorrowingStatusCodes.Active => "info",
            ReservationStatusCodes.Completed or BorrowingStatusCodes.Returned or "RESOLVED" or ConditionCodes.Available or ConditionCodes.Good => "success",
            ReservationStatusCodes.Rejected or BorrowingStatusCodes.Overdue or BorrowingStatusCodes.Lost or ConditionCodes.Damaged or ConditionCodes.UnderMaintenance or "OUT_OF_STOCK"
                or ConditionCodes.Broken or "BROKEN_/_OUT_OF_SERVICE" or ConditionCodes.Retired => "error",
            ConditionCodes.MinorDamage or ConditionCodes.MissingParts => "warning",
            _ => "neutral",
        };
    }
}
