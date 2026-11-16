using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using System;
using System.Linq;

namespace ASI.Basecode.Services.Manager
{
    /// <summary>
    /// Reservation rules shared by the borrower, custodian and lifecycle services so the stock
    /// hold and the review deadline are computed the same way everywhere.
    /// </summary>
    public static class ReservationRules
    {
        public const string Pending = "PENDING";
        public const string Approved = "APPROVED";
        public const string PartiallyReleased = "PARTIALLY_RELEASED";

        /// <summary>Statuses that still own units on the balance's reserved quantity.</summary>
        public static readonly string[] HoldingStatuses = { Pending, Approved, "READY_FOR_RELEASE", PartiallyReleased };

        /// <summary>
        /// Units still held on the balance: the full request while PENDING, otherwise the approved
        /// quantity minus what has already been released.
        /// </summary>
        public static int RemainingHold(Reservation reservation, string statusCode, int releasedSoFar)
        {
            if (!HoldingStatuses.Contains(statusCode))
            {
                return 0;
            }

            if (statusCode == Pending)
            {
                return reservation.RequestedQuantity;
            }

            var approved = reservation.ApprovedQuantity > 0 ? reservation.ApprovedQuantity : reservation.RequestedQuantity;
            return Math.Max(0, approved - releasedSoFar);
        }

        public static int ReleasedSoFar(AllianceDbContext db, long reservationId)
        {
            return db.Borrowings
                .Where(b => b.ReservationId == reservationId)
                .Sum(b => (int?)b.ReleasedQuantity) ?? 0;
        }

        /// <summary>
        /// A PENDING request must be reviewed within <paramref name="reviewHours"/> of submission,
        /// and never later than its requested pickup.
        /// </summary>
        public static DateTime ReviewDeadline(DateTime submittedAtUtc, DateTime requestedStartAtUtc, int reviewHours)
        {
            var bySubmission = submittedAtUtc.AddHours(Math.Max(1, reviewHours));
            return bySubmission < requestedStartAtUtc ? bySubmission : requestedStartAtUtc;
        }

        /// <summary>An APPROVED reservation not picked up within the window after its pickup start expires.</summary>
        public static DateTime PickupDeadline(DateTime requestedStartAtUtc, int reviewHours)
        {
            return requestedStartAtUtc.AddHours(Math.Max(1, reviewHours));
        }

        /// <summary>Short "Expires in 5h" / "Expires in 2d 3h" label; null when already past.</summary>
        public static string FormatTimeLeft(DateTime deadlineUtc, DateTime nowUtc)
        {
            var left = deadlineUtc - nowUtc;
            if (left <= TimeSpan.Zero)
            {
                return null;
            }

            if (left.TotalHours < 1)
            {
                return $"{Math.Max(1, (int)left.TotalMinutes)}m";
            }

            return left.TotalDays >= 1
                ? $"{(int)left.TotalDays}d {left.Hours}h"
                : $"{(int)left.TotalHours}h";
        }
    }
}
