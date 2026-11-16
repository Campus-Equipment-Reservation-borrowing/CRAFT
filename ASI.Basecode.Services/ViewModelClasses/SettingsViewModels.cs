using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ViewModelClasses
{
    public class PolicyFormViewModel
    {
        public long PolicyVersionId { get; set; }
        public string VersionNumber { get; set; }
        public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
        [System.ComponentModel.DataAnnotations.Range(1, 365)]
        public int MaxBorrowDurationDays { get; set; } = 7;
        [System.ComponentModel.DataAnnotations.Range(1, 100)]
        public int MaxActiveBorrowingUnits { get; set; } = 3;
        [System.ComponentModel.DataAnnotations.Range(1, 100)]
        public int MaxUnitsPerBorrower { get; set; } = 3;
        [System.ComponentModel.DataAnnotations.Range(0, 30)]
        public int ReservationLeadTimeDays { get; set; } = 1;
        [System.ComponentModel.DataAnnotations.Range(1, 365)]
        public int MaxAdvanceBookingDays { get; set; } = 30;
        [System.ComponentModel.DataAnnotations.Range(1, 365)]
        public int FacultyMaxBorrowDays { get; set; } = 21;
        [System.ComponentModel.DataAnnotations.Range(0, 1440)]
        public int OverdueGracePeriodMinutes { get; set; } = 0;
        [System.ComponentModel.DataAnnotations.Range(0, 10000)]
        public int LowStockThreshold { get; set; } = 5;
        public bool BlockReservationWhenOverdue { get; set; } = true;
    }

    public class SettingsIndexViewModel
    {
        public PolicyFormViewModel Policy { get; set; }
        public List<BorrowingPolicyVersion> PolicyHistory { get; set; } = new();
        public List<EquipmentCategory> Categories { get; set; } = new();
        public List<Location> Locations { get; set; } = new();
    }

    public class NotificationRowViewModel
    {
        public long NotificationId { get; set; }
        public string NotificationType { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string LinkUrl { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class NotificationsIndexViewModel
    {
        public List<NotificationRowViewModel> Notifications { get; set; } = new();
        public int UnreadCount { get; set; }
    }
}
