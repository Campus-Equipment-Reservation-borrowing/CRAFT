using ASI.Basecode.Data;
using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ViewModelClasses
{
    public class CustodianReservationRowViewModel
    {
        public long ReservationId { get; set; }
        public long? AssignedCustodianUserId { get; set; }
        /// <summary>Null means the reservation is in the shared pool.</summary>
        public string AssignedCustodianName { get; set; }
        public string ReservationNumber { get; set; }
        public string BorrowerName { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public int RequestedQuantity { get; set; }
        public string PickupLocationName { get; set; }
        public string StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public DateTime RequestedStartAtUtc { get; set; }
        public DateTime ExpectedReturnAtUtc { get; set; }
    }

    public class CustodianReservationsIndexViewModel
    {
        public PagedResult<CustodianReservationRowViewModel> Page { get; set; }
        public string Search { get; set; }
        public long? StatusId { get; set; }
        public List<LookupViewModel> StatusOptions { get; set; } = new();
    }

    public class ReservationReviewViewModel
    {
        public long ReservationId { get; set; }
        public string AssignedCustodianName { get; set; }
        public bool CanClaim { get; set; }
        public string ReservationNumber { get; set; }
        public string StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public string BorrowerName { get; set; }
        public string BorrowerRole { get; set; }
        public string BorrowerDepartment { get; set; }
        public string BorrowerEmail { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public string CategoryName { get; set; }
        public string LocationName { get; set; }
        public string PickupLocationName { get; set; }
        public int RequestedQuantity { get; set; }
        public int ApprovedQuantity { get; set; }
        public int HeldQuantity { get; set; }
        public DateTime RequestedStartAtUtc { get; set; }
        public DateTime ExpectedReturnAtUtc { get; set; }
        public string Purpose { get; set; }
        public string AdditionalNotes { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }
    }

    public class ReleaseContextViewModel
    {
        public long ReservationId { get; set; }
        public string ReservationNumber { get; set; }
        public string BorrowerName { get; set; }
        public string BorrowerRole { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public string CategoryName { get; set; }
        public string LocationName { get; set; }
        public string PickupLocationName { get; set; }
        /// <summary>Units still releasable (approved minus already released).</summary>
        public int ApprovedQuantity { get; set; }
        public int AlreadyReleasedQuantity { get; set; }
        public DateTime ExpectedReturnAtUtc { get; set; }
        public bool CanRelease { get; set; }
        public bool ActiveBorrowingExists { get; set; }
        public List<LookupViewModel> ConditionOptions { get; set; } = new();
    }

    /// <summary>One actionable hit for a scanned code.</summary>
    public class ScanMatchViewModel
    {
        /// <summary>RELEASE (open release form), REVIEW (reservation review) or RETURN (record return).</summary>
        public string Kind { get; set; }
        public long Id { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
    }

    /// <summary>Custodian scan page: what a scanned asset code / reservation number resolves to.</summary>
    public class ScanResultViewModel
    {
        public string Code { get; set; }
        public bool Searched { get; set; }
        public string EquipmentName { get; set; }
        public List<ScanMatchViewModel> Matches { get; set; } = new();
    }

    public class ReleaseFormViewModel
    {
        public long ReservationId { get; set; }
        public int ReleasedQuantity { get; set; }
        public long? ReleaseConditionId { get; set; }
        public string ReleaseNotes { get; set; }
    }

    public class ActiveBorrowingRowViewModel
    {
        public long BorrowingId { get; set; }
        public string TransactionNumber { get; set; }
        public string BorrowerName { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public int ReleasedQuantity { get; set; }
        public DateTime ReleasedAtUtc { get; set; }
        public DateTime DueAtUtc { get; set; }
        public int DaysOverdue { get; set; }
        public bool IsOverdue { get; set; }
        public string ReleasedBy { get; set; }
        public string StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public string InspectionStatus { get; set; }
    }

    public class ActiveBorrowingsIndexViewModel
    {
        public PagedResult<ActiveBorrowingRowViewModel> Page { get; set; }
        public string Search { get; set; }
    }
}
