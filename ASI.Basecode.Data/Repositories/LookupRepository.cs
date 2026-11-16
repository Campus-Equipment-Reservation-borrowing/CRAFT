using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.QueryModels;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    /// <summary>
    /// Codebook lookups. Data access only.
    /// </summary>
    public class LookupRepository : BaseRepository, ILookupRepository
    {
        public LookupRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public Role GetTrackedRoleById(long roleId)
        {
            return this.Context.Roles.FirstOrDefault(r => r.RoleId == roleId);
        }

        public Role GetRoleById(long roleId)
        {
            return this.Context.Roles.AsNoTracking().FirstOrDefault(r => r.RoleId == roleId);
        }

        public bool RoleHasCode(long roleId, string roleCode)
        {
            return this.Context.Roles.Any(r => r.RoleId == roleId && r.RoleCode == roleCode);
        }

        public List<LookupOption> GetRoleOptions()
        {
            return this.Context.Roles
                .AsNoTracking()
                .OrderBy(r => r.RoleName)
                .Select(r => new LookupOption { Id = r.RoleId, Text = r.RoleName })
                .ToList();
        }

        public List<LookupOption> GetBorrowerRoleOptions()
        {
            return this.Context.Roles
                .AsNoTracking()
                .Where(r => r.IsBorrowerRole)
                .OrderBy(r => r.RoleName)
                .Select(r => new LookupOption { Id = r.RoleId, Text = r.RoleName })
                .ToList();
        }

        public List<LookupOption> GetActiveDepartmentOptions()
        {
            return this.Context.Departments
                .AsNoTracking()
                .Where(d => d.IsActive)
                .OrderBy(d => d.DepartmentName)
                .Select(d => new LookupOption { Id = d.DepartmentId, Text = d.DepartmentName })
                .ToList();
        }

        public AccountStatus GetTrackedAccountStatusByCode(string code)
        {
            return this.Context.AccountStatuses.FirstOrDefault(s => s.Code == code);
        }

        public AccountStatus GetRequiredTrackedAccountStatusByCode(string code)
        {
            return this.Context.AccountStatuses.First(s => s.Code == code);
        }

        public string GetAccountStatusCodeById(long accountStatusId)
        {
            return this.Context.AccountStatuses.AsNoTracking()
                .Where(st => st.AccountStatusId == accountStatusId).Select(st => st.Code).FirstOrDefault();
        }

        public bool AccountStatusHasCode(long accountStatusId, string code)
        {
            return this.Context.AccountStatuses.Any(s => s.AccountStatusId == accountStatusId && s.Code == code);
        }

        public Dictionary<string, long> GetAccountStatusIdsByCode()
        {
            return this.Context.AccountStatuses
                .AsNoTracking()
                .ToDictionary(s => s.Code, s => s.AccountStatusId);
        }

        public List<LookupOption> GetAccountStatusOptions()
        {
            return this.Context.AccountStatuses
                .AsNoTracking()
                .OrderBy(s => s.Code)
                .Select(s => new LookupOption { Id = s.AccountStatusId, Text = s.Description })
                .ToList();
        }

        public ReservationStatus GetTrackedReservationStatusByCode(string code)
        {
            return this.Context.ReservationStatuses.FirstOrDefault(s => s.Code == code);
        }

        public string GetReservationStatusCodeById(long statusId)
        {
            return this.Context.ReservationStatuses
                .AsNoTracking()
                .Where(s => s.StatusId == statusId)
                .Select(s => s.Code)
                .FirstOrDefault();
        }

        public Dictionary<string, long> GetReservationStatusIdsByCode()
        {
            return this.Context.ReservationStatuses
                .AsNoTracking()
                .ToDictionary(s => s.Code, s => s.StatusId);
        }

        public List<LookupOption> GetReservationStatusOptions()
        {
            return this.Context.ReservationStatuses.AsNoTracking()
                .OrderBy(s => s.StatusId).Select(s => new LookupOption { Id = s.StatusId, Text = s.Description }).ToList();
        }

        public BorrowingStatus GetTrackedBorrowingStatusByCode(string code)
        {
            return this.Context.BorrowingStatuses.FirstOrDefault(s => s.Code == code);
        }

        public InspectionStatus GetTrackedInspectionStatusById(long statusId)
        {
            return this.Context.InspectionStatuses.FirstOrDefault(s => s.StatusId == statusId);
        }

        public InspectionStatus GetTrackedInspectionStatusByCode(string code)
        {
            return this.Context.InspectionStatuses.FirstOrDefault(s => s.Code == code);
        }

        public long GetInspectionStatusIdByCode(string code)
        {
            return this.Context.InspectionStatuses.AsNoTracking()
                .Where(s => s.Code == code).Select(s => s.StatusId).FirstOrDefault();
        }

        public List<LookupOption> GetInspectionStatusOptions()
        {
            return this.Context.InspectionStatuses.AsNoTracking()
                .OrderBy(s => s.StatusId).Select(s => new LookupOption { Id = s.StatusId, Text = s.Description }).ToList();
        }

        public Condition GetConditionById(long conditionId)
        {
            return this.Context.Conditions.AsNoTracking()
                .FirstOrDefault(c => c.ConditionId == conditionId);
        }

        public string GetConditionCodeById(long? conditionId)
        {
            return this.Context.Conditions.AsNoTracking()
                .Where(c => c.ConditionId == conditionId).Select(c => c.Code).FirstOrDefault();
        }

        public long? GetConditionIdByCode(string code)
        {
            return this.Context.Conditions.AsNoTracking()
                .Where(c => c.Code == code).Select(c => (long?)c.ConditionId).FirstOrDefault();
        }

        public bool ConditionExists(long conditionId)
        {
            return this.Context.Conditions.Any(c => c.ConditionId == conditionId);
        }

        public List<LookupOption> GetConditionOptionsWithCodes(IReadOnlyCollection<string> codes)
        {
            return this.Context.Conditions
                .AsNoTracking()
                .Where(c => codes.Contains(c.Code))
                .OrderBy(c => c.ConditionId)
                .Select(c => new LookupOption { Id = c.ConditionId, Text = c.Description })
                .ToList();
        }

        public List<LookupOption> GetConditionOptionsExcludingCodes(IReadOnlyCollection<string> codes, bool orderByCode)
        {
            var query = this.Context.Conditions
                .AsNoTracking()
                .Where(c => !codes.Contains(c.Code));
            query = orderByCode ? query.OrderBy(c => c.Code) : query.OrderBy(c => c.ConditionId);
            return query
                .Select(c => new LookupOption { Id = c.ConditionId, Text = c.Description })
                .ToList();
        }
    }
}
