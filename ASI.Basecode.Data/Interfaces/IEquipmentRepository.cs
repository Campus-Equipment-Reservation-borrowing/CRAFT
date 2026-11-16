using ASI.Basecode.Data.Models;
using System;
using ASI.Basecode.Data.QueryModels;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Repository for equipment aggregate queries and basic data access.
    /// </summary>
    public interface IEquipmentRepository
    {
        /// <summary>
        /// Gets a single equipment record with category, location, condition and balance navigations.
        /// </summary>
        Equipment GetByIdWithDetails(long equipmentId);

        /// <summary>
        /// Gets a single equipment record (tracked) for update scenarios.
        /// </summary>
        Equipment GetTrackedById(long equipmentId);

        /// <summary>
        /// Runs the equipment administration list query with search, codebook filters and paging.
        /// </summary>
        PagedResult<Equipment> GetPaged(
            string search,
            long? categoryId,
            long? locationId,
            long? conditionId,
            long? operationalStatusId,
            bool? isActive,
            int page,
            int pageSize);

        /// <summary>
        /// Checks whether an asset code is already used (excluding a record being edited).
        /// </summary>
        bool AssetCodeExists(string assetCode, long? excludeEquipmentId);

        /// <summary>
        /// True when borrowings, reservations or inspections reference the equipment.
        /// </summary>
        bool HasTransactionReferences(long equipmentId);

        /// <summary>
        /// Aggregate counters for the administration dashboard cards.
        /// </summary>
        EquipmentTotals GetTotals();

        /// <summary>Total and available units of active equipment with a balance, per category name.</summary>
        System.Collections.Generic.List<CategoryUnitTotals> GetActiveCategoryUnitTotals();

        /// <summary>Number of equipment categories.</summary>
        int CountCategories();

        /// <summary>Untracked active equipment with inventory balance, operational status and current condition.</summary>
        System.Collections.Generic.List<Equipment> GetActiveWithStockState();

        /// <summary>Adds equipment (saved by the caller).</summary>
        void Add(Equipment equipment);

        /// <summary>Removes equipment (saved by the caller).</summary>
        void Remove(Equipment equipment);

        /// <summary>All categories (untracked) by name.</summary>
        System.Collections.Generic.List<EquipmentCategory> GetCategoriesByName();

        /// <summary>Active locations (untracked) by name.</summary>
        System.Collections.Generic.List<Location> GetActiveLocationsByName();

        /// <summary>Equipment (untracked) whose asset code matches case-insensitively (pass it upper-cased), or null.</summary>
        Equipment FindByAssetCodeUpper(string upperAssetCode);

        /// <summary>Name of the location, or null.</summary>
        string GetLocationName(long locationId);

        /// <summary>Name of the category, or null.</summary>
        string GetCategoryName(long categoryId);

        /// <summary>Location and category of the equipment; both null when it doesn't exist.</summary>
        (long? LocationId, long? CategoryId) GetLocationAndCategory(long equipmentId);
    }
}
