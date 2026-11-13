using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class BorrowingPolicyVersion
{
    public long PolicyVersionId { get; set; }

    public string VersionNumber { get; set; }

    public DateTime EffectiveFromUtc { get; set; }

    public bool IsCurrent { get; set; }

    public int MaxBorrowDurationDays { get; set; }

    public int MaxActiveBorrowingUnits { get; set; }

    public int OverdueGracePeriodMinutes { get; set; }

    public int LowStockThreshold { get; set; }

    public bool BlockReservationWhenOverdue { get; set; }

    public int MaxUnitsPerBorrower { get; set; } = 3;

    public virtual ICollection<Borrowing> Borrowings { get; set; } = new List<Borrowing>();

    public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
