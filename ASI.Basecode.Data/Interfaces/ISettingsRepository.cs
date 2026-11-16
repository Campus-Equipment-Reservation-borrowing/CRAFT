using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.QueryModels;
using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    public interface ISettingsRepository
    {
        BorrowingPolicyVersion GetCurrentPolicy();

        /// <summary>Overdue grace period of the current policy in minutes, or null when no policy exists.</summary>
        int? GetCurrentOverdueGracePeriodMinutes();

        List<BorrowingPolicyVersion> GetPolicyHistory();

        List<EquipmentCategory> GetCategories();

        List<Location> GetLocations();

        bool CategoryHasEquipment(long categoryId);

        bool LocationHasEquipment(long locationId);
    }
}
