using System;

namespace ASI.Basecode.Data.Models;

/// <summary>
/// Admin-defined responsibility of a custodian (TECHNICIAN) for one borrower account, one
/// location or one equipment category. Each target has at most one custodian, so reservation
/// routing is deterministic. Table created by <c>DatabaseSeeder</c>.
/// </summary>
public partial class CustodianAssignment
{
    public long AssignmentId { get; set; }

    public long CustodianUserId { get; set; }

    /// <summary>BORROWER | LOCATION | CATEGORY — see <c>CustodianScopeTypes</c>.</summary>
    public string ScopeType { get; set; }

    public long? BorrowerUserId { get; set; }

    public long? LocationId { get; set; }

    public long? EquipmentCategoryId { get; set; }

    public long? AssignedByUserId { get; set; }

    public DateTime AssignedAtUtc { get; set; }

    public virtual User CustodianUser { get; set; }

    public virtual User BorrowerUser { get; set; }

    public virtual Location Location { get; set; }

    public virtual EquipmentCategory EquipmentCategory { get; set; }
}
