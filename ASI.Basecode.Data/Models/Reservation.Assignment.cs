namespace ASI.Basecode.Data.Models;

/// <summary>
/// Custodian routing column added on top of the scaffolded reservations table.
/// Null means the reservation sits in the shared pool any custodian can claim.
/// </summary>
public partial class Reservation
{
    public long? AssignedCustodianUserId { get; set; }

    public virtual User AssignedCustodianUser { get; set; }
}
