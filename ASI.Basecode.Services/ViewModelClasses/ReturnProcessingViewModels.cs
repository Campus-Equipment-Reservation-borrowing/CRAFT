using ASI.Basecode.Data;
using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ViewModelClasses
{
    public class ReturnFormViewModel
    {
        public long BorrowingId { get; set; }
        public int ReturnedQuantity { get; set; }
        public DateTime ReturnedAtUtc { get; set; } = DateTime.UtcNow;
        public long ReceivedByUserId { get; set; }
        public string Notes { get; set; }
    }

    public class ReturnFormContextViewModel
    {
        public ReturnFormViewModel Form { get; set; }
        public string TransactionNumber { get; set; }
        public string BorrowerName { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public int ReleasedQuantity { get; set; }
        public int AlreadyReturned { get; set; }
        public DateTime DueAtUtc { get; set; }
        public bool IsOverdue { get; set; }
        public int DaysOverdue { get; set; }
    }

    public class ReturnRowViewModel
    {
        public long ReturnId { get; set; }
        public string TransactionNumber { get; set; }
        public string BorrowerName { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public int ReturnedQuantity { get; set; }
        public DateTime ReturnedAtUtc { get; set; }
        public bool WasReturnedLate { get; set; }
        public int DaysOverdue { get; set; }
        public string ReceivedBy { get; set; }
    }

    public class ReturnsIndexViewModel
    {
        public PagedResult<ReturnRowViewModel> Page { get; set; }
        public string Search { get; set; }
    }

    public class InspectionFormViewModel
    {
        public long BorrowingId { get; set; }
        public long? BorrowingReturnId { get; set; }
        public int InspectedQuantity { get; set; }
        public long CurrentConditionId { get; set; }
        public long? ReleaseConditionId { get; set; }
        public string InspectionNotes { get; set; }
        public long InspectionStatusId { get; set; }

        // Close-out outcome — applied when the inspection status is COMPLETED.
        public int GoodQuantity { get; set; }
        public int DamagedQuantity { get; set; }
        public int LostQuantity { get; set; }
        public decimal? RepairCost { get; set; }
        public decimal? PenaltyAmount { get; set; }
        public string PenaltyNote { get; set; }
    }

    public class InspectionFormContextViewModel
    {
        public InspectionFormViewModel Form { get; set; }
        public string TransactionNumber { get; set; }
        public string BorrowerName { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public int ReleasedQuantity { get; set; }
        public List<LookupViewModel> ConditionOptions { get; set; } = new();
        public List<LookupViewModel> InspectionStatusOptions { get; set; } = new();
    }
}
