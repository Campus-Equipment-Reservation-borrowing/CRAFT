using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ViewModelClasses
{
    public class PolicyFormViewModel
    {
        public long PolicyVersionId { get; set; }
        public string VersionNumber { get; set; }
        public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
        [Range(1, 365, ErrorMessage = "Max borrow days must be between 1 and 365.")]
        public int MaxBorrowDurationDays { get; set; } = 7;
        [Range(1, 1000, ErrorMessage = "Max active units must be at least 1.")]
        public int MaxActiveBorrowingUnits { get; set; } = 3;
        [Range(0, 100000, ErrorMessage = "Overdue grace cannot be negative.")]
        public int OverdueGracePeriodMinutes { get; set; } = 0;
        [Range(0, 100000, ErrorMessage = "Low stock threshold cannot be negative.")]
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
        public bool IsRead { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class NotificationsIndexViewModel
    {
        public List<NotificationRowViewModel> Notifications { get; set; } = new();
        public PagedResult<NotificationRowViewModel> Page { get; set; } = new PagedResult<NotificationRowViewModel>();
        public int UnreadCount { get; set; }
        public string Search { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public List<string> TypeOptions { get; set; } = new();
    }
}
