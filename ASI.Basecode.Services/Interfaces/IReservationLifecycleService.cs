using System;
using ASI.Basecode.Services.ServiceModels;

namespace ASI.Basecode.Services.Interfaces
{
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
