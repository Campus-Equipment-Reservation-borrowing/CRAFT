using System;

namespace ASI.Basecode.Data.QueryModels
{
    /// <summary>
    /// One reservation, release, return or inspection event for transaction monitoring.
    /// </summary>
    public class TransactionRow
    {
        public string Type { get; set; }
        public string Reference { get; set; }
        public DateTime Date { get; set; }
        public string Actor { get; set; }
        public string Equipment { get; set; }
        public string AssetCode { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; }
        public string StatusCode { get; set; }
        /// <summary>Due time (borrowing rows only).</summary>
        public DateTime? DueAtUtc { get; set; }
    }
}
