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

    public virtual User AssignedCustodianUser { get; set; }
}
