using ASI.Basecode.Services.ViewModelClasses;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Authentication and account self-service: screens 1.1 - 1.4.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Validates credentials against <c>users</c>. Accepts school email or account identifier.
        /// </summary>
        (SignInResult Result, AuthenticatedUserViewModel User, string Error) SignIn(string userId, string password);

        /// <summary>
        /// Writes the LOGOUT audit row (never throws).
        /// </summary>
        void RecordSignOut(long userId);

        /// <summary>
        /// Creates a borrower account with status UNVERIFIED. Enforces the [1.1] validation rules.
        /// </summary>
        (bool Success, string Error, long UserId) Register(RegisterViewModel form);

        /// <summary>
        /// Issues a single-use reset token valid for 24 hours.
        /// </summary>
        (bool Success, string Error, string Token) CreatePasswordResetToken(string email);

        /// <summary>
        /// True when the token exists, is unused and has not expired.
        /// </summary>
        bool IsResetTokenValid(string token);

        /// <summary>
        /// Consumes the token and stores the new password hash.
        /// </summary>
        (bool Success, string Error) ResetPassword(ResetPasswordViewModel form);

        /// <summary>
        /// Changes a signed-in user's password after verifying the current one.
        /// </summary>
        (bool Success, string Error) ChangePassword(long userId, string currentPassword, string newPassword);

        /// <summary>
        /// Borrower-selectable roles (Student / Faculty) for the registration dropdown.
        /// </summary>
        List<LookupViewModel> GetBorrowerRoleOptions();

        /// <summary>
        /// Active departments for the registration dropdown.
        /// </summary>
        List<LookupViewModel> GetDepartmentOptions();

        /// <summary>
        /// Re-reads identity from the database — used to refresh the auth cookie.
        /// </summary>
        AuthenticatedUserViewModel GetAuthenticatedUser(long userId);
    }
}
