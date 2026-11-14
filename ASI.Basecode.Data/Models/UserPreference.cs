using System;

namespace ASI.Basecode.Data.Models;

/// <summary>
/// Per-user notification preferences for the Profile &amp; Settings screen
/// (2.10 / 3.10 / 4.8). One row per user.
/// </summary>
public partial class UserPreference
{
    public long UserId { get; set; }

    public bool NotifyReservationUpdates { get; set; }

    public bool NotifyDueReminders { get; set; }

    public bool NotifyOverdueAlerts { get; set; }

    public bool NotifyReturnConfirmations { get; set; }

    public bool NotifyAnnouncements { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>When the user finished or skipped the first-sign-in walkthrough; null until then.</summary>
    public DateTime? TourCompletedAtUtc { get; set; }

    public virtual User User { get; set; }
}
