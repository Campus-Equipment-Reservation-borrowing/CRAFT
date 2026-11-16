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
    }
}
