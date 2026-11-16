using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.QueryModels;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Reads the codebook tables (roles, departments, account/reservation/borrowing/inspection statuses,
    /// conditions). Methods named "Tracked" return entities attached to the context; the rest read without tracking.
    /// </summary>
    public interface ILookupRepository
    {
        // Roles
        Role GetTrackedRoleById(long roleId);
        Role GetRoleById(long roleId);
        bool RoleHasCode(long roleId, string roleCode);
        List<LookupOption> GetRoleOptions();
        List<LookupOption> GetBorrowerRoleOptions();

        // Departments
        List<LookupOption> GetActiveDepartmentOptions();

        // Account statuses
        AccountStatus GetTrackedAccountStatusByCode(string code);

        /// <summary>Same as GetTrackedAccountStatusByCode but throws when the code is missing.</summary>
        AccountStatus GetRequiredTrackedAccountStatusByCode(string code);
        string GetAccountStatusCodeById(long accountStatusId);
        bool AccountStatusHasCode(long accountStatusId, string code);
        Dictionary<string, long> GetAccountStatusIdsByCode();
        List<LookupOption> GetAccountStatusOptions();

        // Reservation statuses
        ReservationStatus GetTrackedReservationStatusByCode(string code);
        string GetReservationStatusCodeById(long statusId);
        Dictionary<string, long> GetReservationStatusIdsByCode();
        List<LookupOption> GetReservationStatusOptions();

        // Borrowing statuses
        BorrowingStatus GetTrackedBorrowingStatusByCode(string code);

        // Inspection statuses
        InspectionStatus GetTrackedInspectionStatusById(long statusId);
        InspectionStatus GetTrackedInspectionStatusByCode(string code);
        long GetInspectionStatusIdByCode(string code);
        List<LookupOption> GetInspectionStatusOptions();

        // Conditions
        Condition GetConditionById(long conditionId);
        string GetConditionCodeById(long? conditionId);
        long? GetConditionIdByCode(string code);
        bool ConditionExists(long conditionId);

        /// <summary>Condition options whose code is in the given set, ordered by id.</summary>
        List<LookupOption> GetConditionOptionsWithCodes(IReadOnlyCollection<string> codes);

        /// <summary>Condition options whose code is not in the given set, ordered by code or by id.</summary>
        List<LookupOption> GetConditionOptionsExcludingCodes(IReadOnlyCollection<string> codes, bool orderByCode);
    }
}
