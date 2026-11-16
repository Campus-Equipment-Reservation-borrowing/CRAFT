using System;

namespace ASI.Basecode.Data.QueryModels
{
    /// <summary>
    /// Flattened borrowing row for the CSV export.
    /// </summary>
    public class BorrowingExportRow
    {
        public string TransactionNumber { get; set; }
        public string Reservation { get; set; }
        public string Borrower { get; set; }
        public string BorrowerId { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public int ReleasedQuantity { get; set; }
        public int Returned { get; set; }
        public DateTime ReleasedAtUtc { get; set; }
        public DateTime DueAtUtc { get; set; }
        public string Status { get; set; }
        public string Inspection { get; set; }
        public string ReleasedBy { get; set; }
    }
}
