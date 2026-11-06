using System;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>Counts from one lifecycle pass (for logging).</summary>
    public class LifecycleRunResult
    {
        public int Expired { get; set; }
        public int MarkedOverdue { get; set; }
        public int DueReminders { get; set; }
    }

    /// <summary>
    /// Time-driven reservation/borrowing transitions: expire stale PENDING reservations (releasing
    /// their held units), persist OVERDUE borrowings past due + grace, and send due-soon reminders.
    /// Each notice is sent once.
    /// </summary>
    public interface IReservationLifecycleService
    {
        LifecycleRunResult RunOnce(DateTime? nowUtc = null);
    }
}
