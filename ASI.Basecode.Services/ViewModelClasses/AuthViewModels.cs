using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ViewModelClasses
{
    /// <summary>
    /// [1.2 Login] — accepts either the school email or the account identifier.
    /// </summary>
    public class LoginViewModel
    {
        [Display(Name = "School Email or Account ID")]
        [Required(ErrorMessage = "Please enter your username and password.")]
        public string UserId { get; set; }

        [Display(Name = "Password")]
        [Required(ErrorMessage = "Please enter your username and password.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }

        /// <summary>Safe local return url honoured after a successful sign in.</summary>
        public string ReturnUrl { get; set; }
    }

    /// <summary>
    /// [1.1 Registration] — self-service signup. Only borrower roles (Student / Faculty)
    /// may be chosen here; Custodian and Administrator are created in [4.1 User Management].
    /// </summary>
    public class RegisterViewModel
    {
        [Display(Name = "First Name")]
        [Required(ErrorMessage = "Please complete all required fields.")]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Display(Name = "Last Name")]
        [Required(ErrorMessage = "Please complete all required fields.")]
        [StringLength(100)]
        public string LastName { get; set; }

        [Display(Name = "School ID / Employee ID")]
        [Required(ErrorMessage = "Please complete all required fields.")]
        [StringLength(100)]
        public string AccountIdentifier { get; set; }

        [Display(Name = "Role")]
        [Required(ErrorMessage = "Please complete all required fields.")]
        public long? RoleId { get; set; }

        [Display(Name = "Department / Program")]
        [Required(ErrorMessage = "Please complete all required fields.")]
        public long? DepartmentId { get; set; }

        [Display(Name = "School Email")]
        [Required(ErrorMessage = "Please complete all required fields.")]
        [EmailAddress(ErrorMessage = "Please use your school-issued email address.")]
        [StringLength(200)]
        public string SchoolEmail { get; set; }

        [Display(Name = "Password")]
        [Required(ErrorMessage = "Please complete all required fields.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Confirm Password")]
        [Required(ErrorMessage = "Please complete all required fields.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }

        [Display(Name = "Data Privacy Consent")]
        [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the Data Privacy consent to continue.")]
        public bool PrivacyConsent { get; set; }

        /// <summary>Borrower role choices (Student / Faculty) for the form dropdown.</summary>
        public List<LookupViewModel> RoleOptions { get; set; } = new List<LookupViewModel>();

        public List<LookupViewModel> DepartmentOptions { get; set; } = new List<LookupViewModel>();
    }

    /// <summary>
    /// [1.3 Forgot Password].
    /// </summary>
    public class ForgotPasswordViewModel
    {
        [Display(Name = "Registered Email")]
        [Required(ErrorMessage = "Please enter your registered email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; }

        /// <summary>Set after a link is issued so the view can render the confirmation state.</summary>
        public bool LinkSent { get; set; }

        /// <summary>
        /// Dev convenience: the generated reset link. No mail transport is configured,
        /// so the link is surfaced in the UI instead of being emailed.
        /// </summary>
        public string GeneratedResetUrl { get; set; }
    }

    /// <summary>
    /// [1.4 Reset Password] — reached only through a valid, unused, unexpired token.
    /// </summary>
    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; }

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

    /// <summary>
    /// First sign-in with an admin-issued temporary password: the user must choose their own.
    /// </summary>
    public class ForcedPasswordChangeViewModel
    {
        [Display(Name = "Temporary Password")]
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

    /// <summary>
    /// Identity resolved by a successful sign in; used to build the auth cookie.
    /// </summary>
    public class AuthenticatedUserViewModel
    {
        public long UserId { get; set; }

        public string FullName { get; set; }

        public string AccountIdentifier { get; set; }

        public string SchoolEmail { get; set; }

        /// <summary>STUDENT | FACULTY | TECHNICIAN | ADMIN.</summary>
        public string RoleCode { get; set; }

        public string RoleName { get; set; }

        public bool IsBorrowerRole { get; set; }

        public string DepartmentName { get; set; }

        /// <summary>ACTIVE | UNVERIFIED | INACTIVE | SUSPENDED | LOCKED.</summary>
        public string AccountStatusCode { get; set; }

        /// <summary>Admin-issued password that must be replaced before using the app.</summary>
        public bool MustChangePassword { get; set; }
    }
}
