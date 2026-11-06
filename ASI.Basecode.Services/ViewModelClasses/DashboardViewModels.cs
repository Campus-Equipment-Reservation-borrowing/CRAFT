using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ViewModelClasses
{
    public class BorrowerBorrowingRowViewModel
    {
        public long BorrowingId { get; set; }
        public string TransactionNumber { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public int Quantity { get; set; }
        public DateTime ReleasedAtUtc { get; set; }
        public DateTime DueAtUtc { get; set; }
        public string StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsDueToday { get; set; }
        public int DaysOverdue { get; set; }
    }

    public class BorrowerReservationRowViewModel
    {
        public long ReservationId { get; set; }
        public string ReservationNumber { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public int Quantity { get; set; }
        public DateTime RequestedStartAtUtc { get; set; }
        public DateTime ExpectedReturnAtUtc { get; set; }
        public string StatusCode { get; set; }
        public string StatusDescription { get; set; }
    }

    public class BorrowerDueRowViewModel
    {
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public DateTime DueAtUtc { get; set; }
        public string StatusCode { get; set; }
        public string StatusDescription { get; set; }
        public bool IsOverdue { get; set; }
        public bool IsDueToday { get; set; }
        public int DaysOverdue { get; set; }
    }

    public class ActivityRowViewModel
    {
        public string Title { get; set; }
        public string Detail { get; set; }
        public DateTime OccurredAtUtc { get; set; }
    }

    public class BorrowerDashboardViewModel
    {
        public long UserId { get; set; }
        public string DisplayName { get; set; }
        public string FirstName { get; set; }
        public string AccountStatus { get; set; }

        /// <summary>Rejection reason when <see cref="AccountStatus"/> is REJECTED.</summary>
        public string VerificationNote { get; set; }

        public int ActiveBorrowings { get; set; }
        public int PendingReservations { get; set; }
        public int ApprovedReady { get; set; }
        public int OverdueItems { get; set; }
        public int CompletedBorrowings { get; set; }

        public List<BorrowerBorrowingRowViewModel> ActiveBorrowingRows { get; set; } = new();
        public List<BorrowerReservationRowViewModel> RecentReservations { get; set; } = new();
        public List<BorrowerDueRowViewModel> UpcomingDue { get; set; } = new();
        public List<NotificationRowViewModel> Notifications { get; set; } = new();
        public List<ActivityRowViewModel> RecentActivity { get; set; } = new();

        public int UnreadNotifications { get; set; }
    }

    public class CustodianTaskRowViewModel
    {
        public string Type { get; set; }
        public string Title { get; set; }
        public string Detail { get; set; }
        public DateTime WhenUtc { get; set; }
        public string LinkAction { get; set; }
        public string LinkController { get; set; }
        public long? LinkId { get; set; }
    }

    public class InventoryAlertRowViewModel
    {
        public long EquipmentId { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public int AvailableUnits { get; set; }
        public int TotalQuantity { get; set; }
        public string OperationalStatus { get; set; }
        public bool IsLowStock { get; set; }
        public bool IsUnderMaintenance { get; set; }
    }

    public class CustodianDashboardViewModel
    {
        public int PendingReservations { get; set; }
        public int ScheduledReleasesToday { get; set; }
        public int ActiveBorrowings { get; set; }
        public int ExpectedReturnsToday { get; set; }
        public int OverdueEquipment { get; set; }
        public int AwaitingInspection { get; set; }
        public int LowStockCount { get; set; }
        public int UnderMaintenanceCount { get; set; }
        public int LowStockThreshold { get; set; }

        public List<CustodianTaskRowViewModel> Tasks { get; set; } = new();
        public List<InventoryAlertRowViewModel> InventoryAlerts { get; set; } = new();
    }
}
