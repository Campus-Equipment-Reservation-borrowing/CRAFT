using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ViewModelClasses
{
    public class BorrowingListRowViewModel
    {
        public long BorrowingId { get; set; }
        public string TransactionNumber { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public int Quantity { get; set; }
        public DateTime ReleasedAtUtc { get; set; }
        public DateTime DueAtUtc { get; set; }
        public string ReleaseConditionName { get; set; }
        public string StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsDueToday { get; set; }
        public int DaysOverdue { get; set; }
    }

    public class BorrowingMineViewModel
    {
        public List<BorrowingListRowViewModel> Borrowings { get; set; } = new();
        public string Search { get; set; }
    }

    public class BorrowingDetailsViewModel
    {
        public long BorrowingId { get; set; }
        public string TransactionNumber { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public string CategoryName { get; set; }
        public string LocationName { get; set; }
        public int Quantity { get; set; }
        public DateTime ReleasedAtUtc { get; set; }
        public string ReleasedBy { get; set; }
        public string ReleaseConditionName { get; set; }
        public DateTime DueAtUtc { get; set; }
        public string StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsReturned { get; set; }
        public int DaysOverdue { get; set; }
        public DateTime? ReturnedAtUtc { get; set; }
        public int? ReturnedQuantity { get; set; }
        public string ReceivedBy { get; set; }
        public string FinalConditionName { get; set; }
        public string InspectionNotes { get; set; }
        public decimal? PenaltyAmount { get; set; }
        public string PenaltyNote { get; set; }
    }

    public class BorrowingHistoryRowViewModel
    {
        public long BorrowingId { get; set; }
        public string TransactionNumber { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public DateTime ReleasedAtUtc { get; set; }
        public DateTime? ReturnedAtUtc { get; set; }
        public string FinalConditionName { get; set; }
        public string InspectionNotes { get; set; }
        public string StatusDescription { get; set; }
    }

    public class BorrowingHistoryViewModel
    {
        public List<BorrowingHistoryRowViewModel> Borrowings { get; set; } = new();
        public string Search { get; set; }
        public string Condition { get; set; }
        public List<string> ConditionOptions { get; set; } = new();
    }
}
