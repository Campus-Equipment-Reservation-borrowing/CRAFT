using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ViewModelClasses
{
    /// <summary>
    /// Row model for the Equipment Administration list (Figma screen A - Equipment Administration).
    /// </summary>
    public class EquipmentListViewModel
    {
        public long EquipmentId { get; set; }

        public string AssetCode { get; set; }

        public string EquipmentName { get; set; }

        public string Description { get; set; }

        public string CategoryName { get; set; }

        public string LocationName { get; set; }

        public string ConditionName { get; set; }

        public string OperationalStatusName { get; set; }

        public int TotalQuantity { get; set; }

        public int AvailableUnits { get; set; }

        public bool IsActive { get; set; }

        public string ImagePath { get; set; }
    }

    /// <summary>
    /// Create/Edit form for an equipment record. Mirrors the Figma "Add / Edit Equipment" form.
    /// </summary>
    public class EquipmentFormViewModel
    {
        public long EquipmentId { get; set; }

        [Display(Name = "Equipment Name")]
        [Required(ErrorMessage = "Equipment name is required.")]
        [StringLength(200, ErrorMessage = "Equipment name must be at most 200 characters long.")]
        public string EquipmentName { get; set; }

        [Display(Name = "Asset Code")]
        [Required(ErrorMessage = "Asset code is required.")]
        [StringLength(50, ErrorMessage = "Asset code must be at most 50 characters long.")]
        public string AssetCode { get; set; }

        [Display(Name = "Description")]
        [StringLength(500, ErrorMessage = "Description must be at most 500 characters long.")]
        public string Description { get; set; }

        [Display(Name = "Category")]
        [Required(ErrorMessage = "Equipment category is required.")]
        public long? EquipmentCategoryId { get; set; }

        [Display(Name = "Location")]
        public long? LocationId { get; set; }

        [Display(Name = "Condition Classification")]
        [Required(ErrorMessage = "Condition classification is required.")]
        public long? CurrentConditionId { get; set; }

        [Display(Name = "Availability Status")]
        [Required(ErrorMessage = "Availability status is required.")]
        public long? OperationalStatusId { get; set; }

        [Display(Name = "Total Quantity")]
        [Required(ErrorMessage = "Total quantity is required.")]
        [Range(1, 10000, ErrorMessage = "Total quantity must be between 1 and 10,000.")]
        public int TotalQuantity { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        /// <summary>Existing/uploaded web-relative image path.</summary>
        public string ImagePath { get; set; }
    }

    /// <summary>
    /// Option item for codebook dropdowns (category, location, condition).
    /// </summary>
    public class LookupViewModel
    {
        public long Id { get; set; }

        public string Text { get; set; }
    }

    /// <summary>
    /// Aggregates everything the Equipment list screen needs.
    /// </summary>
    public class EquipmentIndexViewModel
    {
        public PagedResult<EquipmentListViewModel> Page { get; set; }

        public EquipmentTotals Totals { get; set; }

        /// <summary>Bound filter values</summary>
        public string Search { get; set; }

        public long? CategoryId { get; set; }

        public long? LocationId { get; set; }

        public long? ConditionId { get; set; }

        public long? OperationalStatusId { get; set; }
    }

    /// <summary>
    /// Aggregates dropdown options for the equipment form.
    /// </summary>
    public class EquipmentFormContextViewModel
    {
        public EquipmentFormViewModel Equipment { get; set; }

        public bool IsEdit => Equipment.EquipmentId > 0;
    }
}
