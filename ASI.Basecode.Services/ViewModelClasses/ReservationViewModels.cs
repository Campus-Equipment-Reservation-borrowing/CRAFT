using ASI.Basecode.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ViewModelClasses
{
    /// <summary>
    /// Effective borrowing rules used by reservation checks. Plain carrier class so the
    /// System Settings screen (Phase 7) and the reservation flow share one representation.
    /// Values that have no borrowing_policy_versions column use sensible fixed constants.
    /// </summary>
    public class PolicySettings
    {
        /// <summary>Null when no policy row exists and defaults were applied.</summary>
        public long? PolicyVersionId { get; set; }

        public string VersionNumber { get; set; }

        /// <summary>Standard (non-faculty) maximum borrowing duration in days.</summary>
        public int MaxBorrowDays { get; set; }

        /// <summary>Maximum units a single reservation may request.</summary>
        public int MaxUnitsPerBorrower { get; set; }

        /// <summary>Maximum concurrently open (pending/approved) requests per borrower.</summary>
        public int MaxActiveTransactions { get; set; }

        /// <summary>Minimum notice in days between submitting and the pickup/start time.</summary>
        public int ReservationLeadTimeDays { get; set; }

        /// <summary>How far ahead a borrower may book (days from now).</summary>
        public int MaxAdvanceBookingDays { get; set; }

        /// <summary>Unclaimed pending requests auto-cancel after this many hours.</summary>
        public int AutoCancelHours { get; set; }

        /// <summary>FACULTY borrowers may borrow up to this many days.</summary>
        public int FacultyMaxBorrowDays { get; set; }

        public bool BlockReservationWhenOverdue { get; set; }
    }

    /// <summary>
    /// Row model for the borrower equipment-availability list (Figma: B - Equipment Availability).
    /// </summary>
    public class EquipmentAvailabilityRowViewModel
    {
        public long EquipmentId { get; set; }

        public string AssetCode { get; set; }

        public string EquipmentName { get; set; }

        public string CategoryName { get; set; }

        public string LocationName { get; set; }

        public string ConditionName { get; set; }

        public string OperationalStatusName { get; set; }

        public string ImagePath { get; set; }

        public int TotalQuantity { get; set; }

        public int AvailableUnits { get; set; }
    }

    /// <summary>
    /// Read model for the borrower Equipment Details screen (2.2).
    /// </summary>
    public class EquipmentDetailsViewModel
    {
        public long EquipmentId { get; set; }

        public string EquipmentName { get; set; }

        public string AssetCode { get; set; }

        public string Description { get; set; }

        public string CategoryName { get; set; }

        public string LocationName { get; set; }

        public string ConditionName { get; set; }

        public string OperationalStatusCode { get; set; }

        public string OperationalStatusName { get; set; }

        public string ImagePath { get; set; }

        public int TotalQuantity { get; set; }

        public int ReservedUnits { get; set; }

        public int AvailableUnits { get; set; }

        public bool IsUnderMaintenance { get; set; }

        public bool CanReserve { get; set; }

        /// <summary>Reason the Reserve button is disabled, if any.</summary>
        public string UnavailableReason { get; set; }

        // Borrowing rules from the active policy (4.7 System Settings).
        public int MaxBorrowDays { get; set; }

        public int FacultyMaxBorrowDays { get; set; }

        public int MaxUnitsPerBorrower { get; set; }

        public int ReservationLeadTimeDays { get; set; }
    }

    /// <summary>
    /// Aggregates everything the Equipment Availability screen needs.
    /// </summary>
    public class EquipmentAvailabilityIndexViewModel
    {
        public PagedResult<EquipmentAvailabilityRowViewModel> Page { get; set; }

        /// <summary>Bound filter values</summary>
        public string Search { get; set; }

        public long? CategoryId { get; set; }
    }

    /// <summary>
    /// Reservation form (Figma: B - Reservation Form). Date and time pairs are separate
    /// inputs in the Figma design and are combined to UTC datetimes by the service.
    /// </summary>
    public class ReservationFormViewModel
    {
        public long EquipmentId { get; set; }

        [Display(Name = "Quantity")]
        [Required(ErrorMessage = "Enter how many units you need.")]
        public int? Quantity { get; set; }

        [Display(Name = "Pickup Point")]
        [Required(ErrorMessage = "Choose a pickup point.")]
        public long? PickupLocationId { get; set; }

        [Display(Name = "Borrowing Date")]
        [Required(ErrorMessage = "Choose a borrowing date.")]
        public DateOnly? BorrowingDate { get; set; }

        [Display(Name = "Borrowing Time")]
        [Required(ErrorMessage = "Choose a borrowing time.")]
        public TimeOnly? BorrowingTime { get; set; }

        [Display(Name = "Expected Return Date")]
        [Required(ErrorMessage = "Choose a return date.")]
        public DateOnly? ExpectedReturnDate { get; set; }

        [Display(Name = "Expected Return Time")]
        [Required(ErrorMessage = "Choose a return time.")]
        public TimeOnly? ExpectedReturnTime { get; set; }

        [Display(Name = "Purpose of Borrowing")]
        [Required(ErrorMessage = "Purpose of borrowing is required.")]
        [StringLength(500, ErrorMessage = "Purpose must be at most 500 characters long.")]
        public string Purpose { get; set; }

        [Display(Name = "Additional Notes")]
        [StringLength(500, ErrorMessage = "Additional notes must be at most 500 characters long.")]
        public string AdditionalNotes { get; set; }

        [Display(Name = "Borrowing rules acknowledgement")]
        [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the borrowing rules.")]
        public bool AcknowledgeRules { get; set; }

        [Display(Name = "Responsibility acknowledgement")]
        [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept responsibility for overdue items.")]
        public bool AcknowledgeResponsibility { get; set; }
    }

    /// <summary>
    /// Equipment summary panel shown next to the reservation form (Figma right card).
    /// </summary>
    public class ReservationEquipmentSummaryViewModel
    {
        public long EquipmentId { get; set; }

        public string EquipmentName { get; set; }

        public string AssetCode { get; set; }

        public string CategoryName { get; set; }

        public string LocationName { get; set; }

        public string ConditionName { get; set; }

        public int TotalQuantity { get; set; }

        public int AvailableUnits { get; set; }
    }

    /// <summary>
    /// Aggregates the reservation form screen: the bound form, the summary panel and dropdown options.
    /// </summary>
    public class ReservationFormContextViewModel
    {
        public ReservationFormViewModel Form { get; set; }

        public ReservationEquipmentSummaryViewModel Equipment { get; set; }

        public string OperatorName { get; set; }
    }

    /// <summary>
    /// Row model for the My Reservations list (Figma: B - My Reservations).
    /// </summary>
    public class ReservationListViewModel
    {
        public long ReservationId { get; set; }

        public string ReservationNumber { get; set; }

        public string EquipmentName { get; set; }

        public string AssetCode { get; set; }

        public string CategoryName { get; set; }

        public string PickupLocationName { get; set; }

        public int RequestedQuantity { get; set; }

        public DateTime RequestedStartAtUtc { get; set; }

        public DateTime ExpectedReturnAtUtc { get; set; }

        public string StatusCode { get; set; }

        public string StatusDescription { get; set; }

        public bool CanCancel { get; set; }
    }

    /// <summary>
    /// Aggregates the My Reservations screen.
    /// </summary>
    public class ReservationMineViewModel
    {
        public List<ReservationListViewModel> Reservations { get; set; } = new();

        public string OperatorName { get; set; }
    }

    /// <summary>
    /// Full read model for the reservation details screen (Figma: B - Reservation Details).
    /// </summary>
    public class ReservationDetailsViewModel
    {
        public long ReservationId { get; set; }

        public string ReservationNumber { get; set; }

        public string StatusCode { get; set; }

        public string StatusDescription { get; set; }

        public long EquipmentId { get; set; }

        public string EquipmentName { get; set; }

        public string AssetCode { get; set; }

        public string CategoryName { get; set; }

        public string PickupLocationName { get; set; }

        public int RequestedQuantity { get; set; }

        public int ApprovedQuantity { get; set; }

        public int HeldQuantity { get; set; }

        public DateTime RequestedStartAtUtc { get; set; }

        public DateTime ExpectedReturnAtUtc { get; set; }

        public string Purpose { get; set; }

        public string AdditionalNotes { get; set; }

        public DateTime SubmittedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public string BorrowerName { get; set; }

        public string PolicyVersionNumber { get; set; }

        public bool CanCancel { get; set; }
    }
}
