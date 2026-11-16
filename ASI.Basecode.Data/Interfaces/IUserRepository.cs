using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using ASI.Basecode.Data.QueryModels;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Repository for user aggregate queries and basic data access.
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Gets a single user record with role, department and status navigations.
        /// </summary>
        User GetByIdWithDetails(long userId);

        /// <summary>
        /// Gets a single user record (tracked) for update scenarios.
        /// </summary>
        User GetTrackedById(long userId);

        /// <summary>
        /// Runs the user management list query with search, codebook filters and paging.
        /// </summary>
        PagedResult<User> GetPaged(
            string search,
            long? roleId,
            long? departmentId,
            long? accountStatusId,
            int page,
            int pageSize);

        /// <summary>
        /// Checks whether an account identifier is already used (excluding a record being edited).
        /// Case-insensitive.
        /// </summary>
        bool IdentifierExists(string accountIdentifier, long? excludeUserId);

        /// <summary>
        /// Checks whether a school email is already used (excluding a record being edited).
        /// Case-insensitive.
        /// </summary>
        bool SchoolEmailExists(string schoolEmail, long? excludeUserId);

        /// <summary>
        /// True when borrowings, reservations or inventory movements reference the user.
        /// </summary>
        bool HasTransactionReferences(long userId);

        /// <summary>
        /// Aggregate counters for the user management KPI cards.
        /// </summary>
        UserTotals GetTotals();

        /// <summary>
        /// All user accounts (with role) ordered by name — source for the operator switcher.
        /// </summary>
        List<User> GetOperatorOptions();

        /// <summary>
        /// Id, email and preference row of each of the given users (untracked).
        /// </summary>
        List<NotificationRecipient> GetNotificationRecipients(IReadOnlyCollection<long> userIds);

        /// <summary>
        /// Ids of users holding one of the role codes whose account status has the given code.
        /// </summary>
        List<long> GetUserIdsInRolesWithStatus(IReadOnlyCollection<string> roleCodes, string accountStatusCode);

        /// <summary>
        /// The user (tracked, with role, department and status) whose lower-cased school email or account
        /// identifier equals <paramref name="lowerKey"/>.
        /// </summary>
        User GetTrackedBySignInKeyWithDetails(string lowerKey);

        /// <summary>
        /// The user (tracked, with account status) whose lower-cased school email equals <paramref name="lowerEmail"/>.
        /// </summary>
        User GetTrackedBySchoolEmailWithStatus(string lowerEmail);

        /// <summary>
        /// The user (tracked, with account status), or null.
        /// </summary>
        User GetTrackedByIdWithStatus(long userId);

        /// <summary>
        /// The user's account identifier, or null when the user does not exist.
        /// </summary>
        string GetAccountIdentifier(long userId);

        /// <summary>
        /// True when an account identifier equals this one, compared lower-cased.
        /// </summary>
        bool AccountIdentifierExistsLowercase(string accountIdentifier);

        /// <summary>
        /// True when a school email equals this one, compared lower-cased.
        /// </summary>
        bool SchoolEmailExistsLowercase(string schoolEmail);

        /// <summary>
        /// Adds a user (saved by the caller's SaveChanges).
        /// </summary>
        void Add(User user);

        /// <summary>
        /// Removes a user (saved by the caller's SaveChanges).
        /// </summary>
        void Remove(User user);

        /// <summary>
        /// The user (tracked, with account status and role), or null.
        /// </summary>
        User GetTrackedByIdWithStatusAndRole(long userId);

        /// <summary>
        /// Number of users with the given account status id.
        /// </summary>
        int CountWithAccountStatus(long accountStatusId);

        /// <summary>
        /// The user's role code and account status code, or null when the user does not exist.
        /// </summary>
        UserRoleStatusCodes GetRoleAndStatusCodes(long userId);

        /// <summary>
        /// True when a user other than <paramref name="excludeUserId"/> has the role code and account status code.
        /// </summary>
        bool OtherUserWithRoleAndStatusExists(long excludeUserId, string roleCode, string accountStatusCode);

        /// <summary>
        /// The user's profile image path, or null.
        /// </summary>
        string GetProfileImagePath(long userId);

        /// <summary>
        /// Clears the creator on the user's announcements (tracked; saved by the caller).
        /// </summary>
        void ClearAnnouncementCreator(long userId);

        /// <summary>
        /// Clears the assignee on support tickets assigned to the user (tracked; saved by the caller).
        /// </summary>
        void ClearSupportTicketAssignee(long userId);

        /// <summary>Number of users per account status code.</summary>
        Dictionary<string, int> CountByAccountStatusCode();

        /// <summary>Created and verified timestamps of users that were verified (and have a creation time).</summary>
        List<(DateTime CreatedAtUtc, DateTime VerifiedAtUtc)> GetVerificationTimes();

        /// <summary>Account status code of the user, or null.</summary>
        string GetAccountStatusCode(long userId);

        /// <summary>Role code of the user, or null.</summary>
        string GetRoleCode(long userId);

        /// <summary>"First Last" of the user, or null.</summary>
        string GetFullName(long userId);

        /// <summary>True when the user exists with the given account status code.</summary>
        bool HasAccountStatus(long userId, string accountStatusCode);
    }
}
