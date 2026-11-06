using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ViewModelClasses
{
    public class CustodianAssignmentRowViewModel
    {
        public long AssignmentId { get; set; }
        public string ScopeType { get; set; }
        public long CustodianUserId { get; set; }
        public string CustodianName { get; set; }
        public long TargetId { get; set; }
        public string TargetName { get; set; }
        /// <summary>Secondary text: account ID for borrowers, code for locations/categories.</summary>
        public string TargetDetail { get; set; }
        public DateTime AssignedAtUtc { get; set; }
    }

    /// <summary>Admin → Custodian Assignments screen.</summary>
    public class CustodianAssignmentIndexViewModel
    {
        public List<CustodianAssignmentRowViewModel> BorrowerAssignments { get; set; } = new();
        public List<CustodianAssignmentRowViewModel> LocationAssignments { get; set; } = new();
        public List<CustodianAssignmentRowViewModel> CategoryAssignments { get; set; } = new();

        public List<LookupViewModel> CustodianOptions { get; set; } = new();
        public List<LookupViewModel> UnassignedBorrowerOptions { get; set; } = new();
        public List<LookupViewModel> UnassignedLocationOptions { get; set; } = new();
        public List<LookupViewModel> UnassignedCategoryOptions { get; set; } = new();
    }

    /// <summary>One scope section (borrowers / locations / categories) of the assignments screen.</summary>
    public class AssignmentSectionViewModel
    {
        public string Title { get; set; }
        public string Hint { get; set; }
        public string ScopeType { get; set; }
        public string TargetLabel { get; set; }
        public List<CustodianAssignmentRowViewModel> Rows { get; set; } = new();
        public List<LookupViewModel> TargetOptions { get; set; } = new();
        public List<LookupViewModel> CustodianOptions { get; set; } = new();
    }

    /// <summary>Users/Details summary: what a custodian covers, or who covers a borrower.</summary>
    public class UserAssignmentSummaryViewModel
    {
        public bool IsCustodian { get; set; }
        public bool IsBorrower { get; set; }
        public List<CustodianAssignmentRowViewModel> Assignments { get; set; } = new();
        public string AssignedCustodianName { get; set; }
        public long? AssignedCustodianUserId { get; set; }
    }

    public class CustodianWorkloadRowViewModel
    {
        public long CustodianUserId { get; set; }
        public string CustodianName { get; set; }
        public int Pending { get; set; }
        public int Approved { get; set; }
        public int AssignmentCount { get; set; }
    }

    public class OpenReservationRowViewModel
    {
        public long ReservationId { get; set; }
        public string ReservationNumber { get; set; }
        public string BorrowerName { get; set; }
        public string EquipmentName { get; set; }
        public string StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public long? AssignedCustodianUserId { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
    }

    /// <summary>Monitoring dashboard: custodian workload plus reassignable open reservations.</summary>
    public class CustodianWorkloadViewModel
    {
        public List<CustodianWorkloadRowViewModel> Rows { get; set; } = new();
        public int PoolPending { get; set; }
        public int PoolApproved { get; set; }
        public List<OpenReservationRowViewModel> OpenReservations { get; set; } = new();
        public List<LookupViewModel> CustodianOptions { get; set; } = new();
    }
}
