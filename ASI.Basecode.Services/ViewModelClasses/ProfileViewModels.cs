using System;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ViewModelClasses
{
    /// <summary>
    /// Profile &amp; settings screen (2.10 borrower / 3.10 custodian / 4.8 administrator).
    /// </summary>
    public class ProfileViewModel
    {
        public long UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string FullName { get; set; }
        public string AccountIdentifier { get; set; }
        public string SchoolEmail { get; set; }
        public string RoleCode { get; set; }
        public string RoleName { get; set; }
        public string DepartmentName { get; set; }
        public string AccountStatusCode { get; set; }
        public string AccountStatusDescription { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public string ContactNumber { get; set; }
        public string AlternateContact { get; set; }

        /// <summary>Web-relative path to the profile photo; null when the user has none.</summary>
        public string ProfileImagePath { get; set; }

        public int ActiveBorrowings { get; set; }
        public int CompletedBorrowings { get; set; }
        public int OverdueItems { get; set; }

        public bool NotifyReservationUpdates { get; set; }
        public bool NotifyDueReminders { get; set; }
        public bool NotifyOverdueAlerts { get; set; }
        public bool NotifyReturnConfirmations { get; set; }
        public bool NotifyAnnouncements { get; set; }
    }

    /// <summary>
    /// Contact + notification preference form. Password fields are handled separately.
    /// </summary>
    public class ProfileContactFormViewModel
    {
        [Display(Name = "Contact Number")]
        [Required(ErrorMessage = "Please complete all required fields.")]
        [RegularExpression(@"^[0-9+\-\s()]{7,50}$", ErrorMessage = "Please enter a valid contact number.")]
        public string ContactNumber { get; set; }

        [Display(Name = "Alternate Contact")]
        [StringLength(100)]
        public string AlternateContact { get; set; }

        public bool NotifyReservationUpdates { get; set; }
        public bool NotifyDueReminders { get; set; }
        public bool NotifyOverdueAlerts { get; set; }
        public bool NotifyReturnConfirmations { get; set; }
        public bool NotifyAnnouncements { get; set; }
    }

    /// <summary>
    /// Password change form for the profile screen.
    /// </summary>
    public class ProfilePasswordFormViewModel
    {
        [Display(Name = "Current Password")]
        [Required(ErrorMessage = "Please complete all required fields.")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; }

        [Display(Name = "New Password")]
        [Required(ErrorMessage = "Please complete all required fields.")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [Display(Name = "Confirm New Password")]
        [Required(ErrorMessage = "Please complete all required fields.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }
}
