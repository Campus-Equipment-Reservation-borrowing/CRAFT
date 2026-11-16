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

        /// <summary>Low-stock threshold of the newest current policy (by effective date), or null when none is current.</summary>
        int? GetCurrentLowStockThreshold();

        /// <summary>Adds a policy version (saved by the caller).</summary>
        void AddPolicy(BorrowingPolicyVersion policy);

        /// <summary>True when a category code matches (case-insensitive; pass the code upper-cased).</summary>
        bool CategoryCodeExists(string upperCode);

        /// <summary>True when a category name matches (case-insensitive; pass the name upper-cased).</summary>
        bool CategoryNameExists(string upperName);

        void AddCategory(EquipmentCategory category);

        EquipmentCategory GetTrackedCategory(long categoryId);

        int CountEquipmentInCategory(long categoryId);

        void RemoveCategory(EquipmentCategory category);

        /// <summary>True when a location code matches (case-insensitive; pass the code upper-cased).</summary>
        bool LocationCodeExists(string upperCode);

        void AddLocation(Location location);

        Location GetTrackedLocation(long locationId);

        void RemoveLocation(Location location);

        /// <summary>Resets a tracked entity to Unchanged (undoes a pending add/remove after a failed save).</summary>
        void MarkUnchanged(object entity);
    }
}
