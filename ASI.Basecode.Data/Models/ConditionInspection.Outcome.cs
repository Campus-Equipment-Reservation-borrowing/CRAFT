namespace ASI.Basecode.Data.Models;

/// <summary>
/// Close-out outcome recorded when an inspection is finalized (COMPLETED): how the returned
/// units split between good / damaged / lost, and any repair cost or penalty.
/// Columns added by <c>DatabaseSeeder</c> on top of the scaffolded table.
/// </summary>
public partial class ConditionInspection
{
    public int? GoodQuantity { get; set; }

    public int? DamagedQuantity { get; set; }

    public int? LostQuantity { get; set; }

    public decimal? RepairCost { get; set; }

    public decimal? PenaltyAmount { get; set; }

    public string PenaltyNote { get; set; }
}
