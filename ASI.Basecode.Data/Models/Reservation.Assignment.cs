namespace ASI.Basecode.Data.Models;

/// <summary>
/// Custodian routing column added on top of the scaffolded reservations table.
/// Null means the reservation sits in the shared pool any custodian can claim.
/// </summary>
public partial class Reservation
{
    public long? AssignedCustodianUserId { get; set; }

    /// <summary>When the custodian was warned that this PENDING request is about to expire unreviewed.</summary>
    public System.DateTime? ExpiryWarningSentAtUtc { get; set; }

    /// <summary>Why the reservation was rejected, cancelled or expired; shown to the borrower.</summary>
    public string StatusReason { get; set; }

    /// <summary>Borrower's optional remarks for the custodian, entered on the reservation form.</summary>
    public string AdditionalNotes { get; set; }

    /// <summary>Custodian's notes for the borrower, entered when approving.</summary>
    public string CustodianNotes { get; set; }

    public virtual User AssignedCustodianUser { get; set; }
}
