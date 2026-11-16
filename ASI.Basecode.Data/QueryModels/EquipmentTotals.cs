namespace ASI.Basecode.Data.QueryModels
{
    /// <summary>
    /// Aggregate unit counters across all equipment inventory balances.
    /// </summary>
    public class EquipmentTotals
    {
        public int RecordCount { get; set; }

        public int TotalUnits { get; set; }

        public int ReservedUnits { get; set; }

        public int ReleasedUnits { get; set; }

        public int AwaitingInspectionUnits { get; set; }

        public int MaintenanceUnits { get; set; }

        public int UnreconciledUnits { get; set; }

        /// <summary>
        /// Units genuinely available for a new reservation:
        /// total minus everything held in any downstream state.
        /// </summary>
        public int AvailableUnits =>
            TotalUnits - ReservedUnits - ReleasedUnits - AwaitingInspectionUnits - MaintenanceUnits - UnreconciledUnits;
    }
}
