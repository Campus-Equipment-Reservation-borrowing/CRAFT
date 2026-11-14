using ASI.Basecode.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace ASI.Basecode.Services.Manager
{
    /// <summary>
    /// The one definition of "overdue" and "today" used by every screen, so dashboards, lists,
    /// monitoring and the reservation gate always agree with the lifecycle sweep.
    /// <para>
    /// Overdue: an open borrowing (ACTIVE, OVERDUE, PARTIALLY_RETURNED) whose due time is earlier than
    /// now minus the current policy's overdue grace period.
    /// </para>
    /// <para>
    /// Today: the campus (server-local) calendar day — the same clock the forms read and the views render —
    /// not the UTC date, which rolls over mid-morning in UTC+8.
    /// </para>
    /// </summary>
    public static class BorrowingRules
    {
        /// <summary>Borrowing statuses with units still out.</summary>
        public static readonly string[] OpenStatusCodes = { "ACTIVE", "OVERDUE", "PARTIALLY_RETURNED" };

        /// <summary>Overdue grace period from the current policy, in minutes (0 when no policy).</summary>
        public static int GetGraceMinutes(AllianceDbContext db)
        {
            var grace = db.BorrowingPolicyVersions.AsNoTracking()
                .OrderByDescending(p => p.IsCurrent)
                .ThenByDescending(p => p.PolicyVersionId)
                .Select(p => (int?)p.OverdueGracePeriodMinutes)
                .FirstOrDefault() ?? 0;
            return Math.Max(0, grace);
        }

        /// <summary>Borrowings due before this instant are overdue (now minus the grace period).</summary>
        public static DateTime GetOverdueCutoffUtc(AllianceDbContext db, DateTime nowUtc)
        {
            return nowUtc.AddMinutes(-GetGraceMinutes(db));
        }

        public static bool IsOpen(string statusCode)
        {
            return statusCode != null && OpenStatusCodes.Contains(statusCode);
        }

        public static bool IsOverdue(string statusCode, DateTime dueAtUtc, DateTime overdueCutoffUtc)
        {
            return IsOpen(statusCode) && dueAtUtc < overdueCutoffUtc;
        }

        /// <summary>Whole days past the due time (0 when not yet a full day late).</summary>
        public static int DaysOverdue(DateTime dueAtUtc, DateTime nowUtc)
        {
            return dueAtUtc < nowUtc ? (int)Math.Floor((nowUtc - dueAtUtc).TotalDays) : 0;
        }

        /// <summary>Campus-local calendar date of a UTC instant.</summary>
        public static DateTime LocalDate(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.Local).Date;
        }

        public static bool IsLocalToday(DateTime utc, DateTime nowUtc)
        {
            return LocalDate(utc) == LocalDate(nowUtc);
        }

        /// <summary>UTC bounds [start, end) of the campus-local day containing <paramref name="nowUtc"/>, for queries.</summary>
        public static (DateTime StartUtc, DateTime EndUtc) LocalDayRangeUtc(DateTime nowUtc)
        {
            var localDay = LocalDate(nowUtc);
            var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDay, DateTimeKind.Unspecified), TimeZoneInfo.Local);
            var end = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(localDay.AddDays(1), DateTimeKind.Unspecified), TimeZoneInfo.Local);
            return (start, end);
        }
    }
}
