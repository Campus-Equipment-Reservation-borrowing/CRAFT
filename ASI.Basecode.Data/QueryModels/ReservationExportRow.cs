using System;

namespace ASI.Basecode.Data.QueryModels
{
    /// <summary>
    /// Flattened reservation row for the CSV export.
    /// </summary>
    public class ReservationExportRow
    {
        public string ReservationNumber { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public string Borrower { get; set; }
        public string BorrowerId { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public int RequestedQuantity { get; set; }
        public int ApprovedQuantity { get; set; }
        public string Status { get; set; }
        public string Custodian { get; set; }
        public DateTime RequestedStartAtUtc { get; set; }
        public DateTime ExpectedReturnAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }
}
