using ASI.Basecode.Data;
using ASI.Basecode.Services.ViewModelClasses;
using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ViewModelClasses
{
    public class DashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int EquipmentRecords { get; set; }
        public int TotalUnits { get; set; }
        public int AvailableUnits { get; set; }
        public int ReservedUnits { get; set; }
        public int BorrowedUnits { get; set; }
        public int DamagedLostUnits { get; set; }
        public int ReservationTotal { get; set; }
        public int ReservationReadyForRelease { get; set; }
        public int ReservationPending { get; set; }
        public int ReservationApproved { get; set; }
        public int ReservationRejected { get; set; }
        public int ReservationCancelled { get; set; }
        public int ReservationCompleted { get; set; }
        public int ReservationExpired { get; set; }
        public int BorrowingTotal { get; set; }
        public int BorrowingActive { get; set; }
        public int BorrowingPartial { get; set; }
        public int BorrowingReturned { get; set; }
        public int BorrowingOverdue { get; set; }
        public int BorrowingLost { get; set; }
        public List<AuditRowViewModel> RecentAudit { get; set; } = new();
    }

    public class TransactionRowViewModel
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
    }

    public class TransactionMonitoringViewModel
    {
        public PagedResult<TransactionRowViewModel> Page { get; set; }
        public string Search { get; set; }
        public string Tab { get; set; }
    }

    public class AuditRowViewModel
    {
        public long AuditLogId { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string ActorName { get; set; }
        public string ActionType { get; set; }
        public string EntityType { get; set; }
        public string ReferenceNumber { get; set; }
        public string Details { get; set; }
    }

    public class AuditLogsViewModel
    {
        public PagedResult<AuditRowViewModel> Page { get; set; }
        public string Search { get; set; }
        public string EntityType { get; set; }
        public List<string> EntityTypes { get; set; } = new();
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string User { get; set; }
        public string Role { get; set; }
        public string ActionType { get; set; }
    }
}
