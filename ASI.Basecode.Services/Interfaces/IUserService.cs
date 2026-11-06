using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ViewModelClasses;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// User management CRUD service.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Paged, filtered user list plus KPI counters.
        /// </summary>
        UserIndexViewModel GetIndexViewModel(
            string search,
            long? roleId,
            long? departmentId,
            long? accountStatusId,
            int page,
            int pageSize);

        /// <summary>
        /// Single user record with role, department and status data.
        /// </summary>
        User GetByIdWithDetails(long userId);

        /// <summary>
        /// Form-bound edit model for an existing record (null when missing).
        /// </summary>
        UserFormViewModel GetForEdit(long userId);

        /// <summary>
        /// Creates or updates a user account (based on UserId) enforcing the
        /// account identifier / school email uniqueness rules and writes an audit log entry.
        /// </summary>
        (bool Success, string Error, long UserId) Save(UserFormViewModel form);

        /// <summary>
        /// Deletes a user account when no transactional records reference it.
        /// </summary>
        (bool Success, string Error) Delete(long userId);

        /// <summary>
        /// Role dropdown options.
        /// </summary>
        List<LookupViewModel> GetRoleOptions();

        /// <summary>
        /// Active department dropdown options.
        /// </summary>
        List<LookupViewModel> GetDepartmentOptions();

        /// <summary>
        /// Account status dropdown options.
        /// </summary>
        List<LookupViewModel> GetAccountStatusOptions();

        /// <summary>
        /// All user accounts with role info (ordered by name) for the operator switcher.
        /// </summary>
        List<UserListViewModel> GetOperatorOptions();

        /// <summary>
        /// Verification queue: UNVERIFIED accounts ("pending" tab) or REJECTED ones ("rejected" tab).
        /// </summary>
        VerificationQueueViewModel GetVerificationQueue(string tab, string search, int page, int pageSize);

        /// <summary>
        /// Approves an UNVERIFIED or REJECTED account (status ACTIVE), audits VERIFY and notifies the borrower.
        /// </summary>
        (bool Success, string Error) Verify(long userId);

        /// <summary>
        /// Rejects an UNVERIFIED account with a required reason, audits REJECT and notifies the borrower.
        /// </summary>
        (bool Success, string Error) RejectVerification(long userId, string reason);

        /// <summary>
        /// Issues a new temporary password (must be changed at next sign-in), clears any lockout,
        /// and audits PASSWORD_RESET. Returns the plain temporary password to show the admin once.
        /// </summary>
        (bool Success, string Error, string TemporaryPassword) ResetPassword(long userId);
    }
}
