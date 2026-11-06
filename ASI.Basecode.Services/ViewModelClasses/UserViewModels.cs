using ASI.Basecode.Data;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ViewModelClasses
{
    /// <summary>
    /// Row model for the User Management list (Figma screen A - User Management).
    /// </summary>
    public class UserListViewModel
    {
        public long UserId { get; set; }

        public string AccountIdentifier { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string SchoolEmail { get; set; }

        public string RoleName { get; set; }

        public string RoleCode { get; set; }

        public string DepartmentName { get; set; }

        /// <summary>Account status code: ACTIVE / UNVERIFIED / REJECTED / INACTIVE / SUSPENDED / LOCKED.</summary>
        public string AccountStatusCode { get; set; }

        public string AccountStatusDescription { get; set; }

        public System.DateTime CreatedAtUtc { get; set; }

        /// <summary>Rejection reason recorded by the verification queue.</summary>
        public string VerificationNote { get; set; }
    }

    /// <summary>
    /// Pending Verifications queue (UNVERIFIED accounts) with a Rejected tab.
    /// </summary>
    public class VerificationQueueViewModel
    {
        public PagedResult<UserListViewModel> Page { get; set; }

        /// <summary>"pending" (UNVERIFIED) or "rejected" (REJECTED).</summary>
        public string Tab { get; set; }

        public string Search { get; set; }

        public int PendingCount { get; set; }

        public int RejectedCount { get; set; }
    }

    /// <summary>
    /// Create/Edit form for a user account. Mirrors the Figma "Add / Edit User" form.
    /// Password/auth fields are intentionally out of scope (auth stripped).
    /// </summary>
    public class UserFormViewModel
    {
        public long UserId { get; set; }

        [Display(Name = "First Name")]
        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100, ErrorMessage = "First name must be at most 100 characters long.")]
        public string FirstName { get; set; }

        [Display(Name = "Last Name")]
        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100, ErrorMessage = "Last name must be at most 100 characters long.")]
        public string LastName { get; set; }

        [Display(Name = "Account Identifier")]
        [Required(ErrorMessage = "Account identifier is required.")]
        [StringLength(100, ErrorMessage = "Account identifier must be at most 100 characters long.")]
        public string AccountIdentifier { get; set; }

        [Display(Name = "School Email")]
        [Required(ErrorMessage = "School email is required.")]
        [EmailAddress(ErrorMessage = "School email must be a valid email address.")]
        [StringLength(200, ErrorMessage = "School email must be at most 200 characters long.")]
        public string SchoolEmail { get; set; }

        [Display(Name = "Role")]
        [Required(ErrorMessage = "Role is required.")]
        public long? RoleId { get; set; }

        [Display(Name = "Department")]
        public long? DepartmentId { get; set; }

        [Display(Name = "Account Status")]
        [Required(ErrorMessage = "Account status is required.")]
        public long? AccountStatusId { get; set; }

        /// <summary>
        /// Create only: optional admin-chosen temporary password. Left blank, a strong one is generated.
        /// The user must replace it at first sign-in.
        /// </summary>
        [Display(Name = "Temporary Password")]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "Temporary password must be at most 100 characters long.")]
        public string TemporaryPassword { get; set; }

        /// <summary>Output: the temporary password actually issued on create (shown to the admin once).</summary>
        public string IssuedTemporaryPassword { get; set; }
    }

    /// <summary>
    /// Aggregates everything the User Management list screen needs.
    /// </summary>
    public class UserIndexViewModel
    {
        public PagedResult<UserListViewModel> Page { get; set; }

        public UserTotals Totals { get; set; }

        /// <summary>Bound filter values</summary>
        public string Search { get; set; }

        public long? RoleId { get; set; }

        public long? DepartmentId { get; set; }

        public long? AccountStatusId { get; set; }

        public int PageSize { get; set; }
    }

    /// <summary>
    /// Aggregates dropdown options for the user form.
    /// </summary>
    public class UserFormContextViewModel
    {
        public UserFormViewModel User { get; set; }

        public bool IsEdit => User.UserId > 0;
    }
}
