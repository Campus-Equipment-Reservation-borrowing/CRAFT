using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using ASI.Basecode.Data.QueryModels;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Data.Repositories
{
    /// <summary>
    /// User repository implementation. Queries only — business rules live in services.
    /// </summary>
    public class UserRepository : BaseRepository, IUserRepository
    {
        public UserRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public User GetByIdWithDetails(long userId)
        {
            return this.Context.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.AccountStatus)
                .AsNoTracking()
                .FirstOrDefault(u => u.UserId == userId);
        }

        public User GetTrackedById(long userId)
        {
            return this.Context.Users
                .FirstOrDefault(u => u.UserId == userId);
        }

        public PagedResult<User> GetPaged(
            string search,
            long? roleId,
            long? departmentId,
            long? accountStatusId,
            int page,
            int pageSize)
        {
            const int DefaultPage = 1;
            const int DefaultPageSize = 10;

            var query = this.Context.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.AccountStatus)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim()}%";
                query = query.Where(u =>
                    EF.Functions.Like(u.FirstName, pattern) ||
                    EF.Functions.Like(u.LastName, pattern) ||
                    EF.Functions.Like(u.AccountIdentifier, pattern) ||
                    EF.Functions.Like(u.SchoolEmail, pattern));
            }

            if (roleId.HasValue)
            {
                query = query.Where(u => u.RoleId == roleId.Value);
            }

            if (departmentId.HasValue)
            {
                query = query.Where(u => u.DepartmentId == departmentId.Value);
            }

            if (accountStatusId.HasValue)
            {
                query = query.Where(u => u.AccountStatusId == accountStatusId.Value);
            }

            var totalCount = query.Count();

            page = page < 1 ? DefaultPage : page;
            pageSize = pageSize < 1 ? DefaultPageSize : pageSize;

            var items = query
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ThenBy(u => u.UserId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<User>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
        }

        public bool IdentifierExists(string accountIdentifier, long? excludeUserId)
        {
            var identifier = accountIdentifier?.Trim();
            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            var upper = identifier.ToUpper();
            return this.Context.Users
                .Any(u => u.AccountIdentifier.ToUpper() == upper
                    && (!excludeUserId.HasValue || u.UserId != excludeUserId.Value));
        }

        public bool SchoolEmailExists(string schoolEmail, long? excludeUserId)
        {
            var email = schoolEmail?.Trim();
            if (string.IsNullOrEmpty(email))
            {
                return false;
            }

            var upper = email.ToUpper();
            return this.Context.Users
                .Any(u => u.SchoolEmail.ToUpper() == upper
                    && (!excludeUserId.HasValue || u.UserId != excludeUserId.Value));
        }

        public bool HasTransactionReferences(long userId)
        {
            var referencesBorrowing = this.Context.Borrowings
                .Any(b => b.BorrowerUserId == userId || b.ReleasedByUserId == userId);
            var referencesReservation = this.Context.Reservations
                .Any(r => r.BorrowerUserId == userId || r.AssignedCustodianUserId == userId);
            var referencesMovement = this.Context.InventoryMovements
                .Any(m => m.PerformedByUserId == userId);

            var referencesSupport = this.Context.SupportTickets.Any(t => t.RequesterUserId == userId)
                || this.Context.SupportTicketReplies.Any(r => r.AuthorUserId == userId);

            var referencesReturn = this.Context.BorrowingReturns.Any(r => r.ReceivedByUserId == userId)
                || this.Context.ConditionInspections.Any(i => i.InspectedByUserId == userId);

            return referencesBorrowing || referencesReservation || referencesMovement || referencesSupport || referencesReturn;
        }

        public UserTotals GetTotals()
        {
            return new UserTotals
            {
                TotalAccounts = this.Context.Users.Count(),
                Students = this.Context.Users.Count(u => u.Role.RoleCode == RoleCodes.Student),
                Faculty = this.Context.Users.Count(u => u.Role.RoleCode == RoleCodes.Faculty),
                CustodianAdmins = this.Context.Users.Count(u => u.Role.RoleCode == RoleCodes.Technician || u.Role.RoleCode == RoleCodes.Admin),
                Unverified = this.Context.Users.Count(u => u.AccountStatus.Code == "UNVERIFIED"),
            };
        }

        /// <inheritdoc />
        public List<User> GetOperatorOptions()
        {
            // AccountStatus is included because the list-row map resolves its code/description.
            return this.Context.Users
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.AccountStatus)
                .AsNoTracking()
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .ThenBy(u => u.UserId)
                .ToList();
        }

        public List<NotificationRecipient> GetNotificationRecipients(IReadOnlyCollection<long> userIds)
        {
            return this.Context.Users
                .AsNoTracking()
                .Include(u => u.Preference)
                .Where(u => userIds.Contains(u.UserId))
                .Select(u => new NotificationRecipient { UserId = u.UserId, SchoolEmail = u.SchoolEmail, Preference = u.Preference })
                .ToList();
        }

        public List<long> GetUserIdsInRolesWithStatus(IReadOnlyCollection<string> roleCodes, string accountStatusCode)
        {
            return this.Context.Users
                .AsNoTracking()
                .Where(u => roleCodes.Contains(u.Role.RoleCode) && u.AccountStatus.Code == accountStatusCode)
                .Select(u => u.UserId)
                .ToList();
        }
    }
}
