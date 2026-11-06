using System;

namespace ASI.Basecode.Data.Models;

/// <summary>
/// Reminder bookkeeping added on top of the scaffolded borrowings table so the lifecycle job
/// sends each due-soon / overdue notice once.
/// </summary>
public partial class Borrowing
{
    public DateTime? DueReminderSentAtUtc { get; set; }

    public DateTime? OverdueNotifiedAtUtc { get; set; }
}
