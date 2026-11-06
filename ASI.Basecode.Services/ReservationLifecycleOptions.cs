namespace ASI.Basecode.Services
{
    /// <summary>
    /// Settings for the reservation lifecycle job, bound from the <c>ReservationLifecycle</c>
    /// section of appsettings. The borrowing policy table has no auto-cancel column, so the window
    /// lives here.
    /// </summary>
    public class ReservationLifecycleOptions
    {
        public const string SectionName = "ReservationLifecycle";

        /// <summary>Turn the background job off (e.g. while debugging data by hand).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>PENDING reservations older than this are expired and their units released.</summary>
        public int AutoCancelHours { get; set; } = 24;

        /// <summary>How far ahead of the due date the borrower gets a reminder.</summary>
        public int DueReminderHours { get; set; } = 24;

        /// <summary>Minutes between job runs.</summary>
        public int IntervalMinutes { get; set; } = 5;
    }
}
