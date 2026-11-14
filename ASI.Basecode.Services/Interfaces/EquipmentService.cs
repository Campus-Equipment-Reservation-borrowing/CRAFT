using ASI.Basecode.Data;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.ViewModelClasses;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Equipment administration CRUD service.
    /// </summary>
    public interface IEquipmentService
    {
        /// <summary>
        /// Paged, filtered equipment list plus dashboard counters.
        /// </summary>
        EquipmentIndexViewModel GetIndexViewModel(
            string search,
            long? categoryId,
            long? locationId,
            long? conditionId,
            long? operationalStatusId,
            int page,
            int pageSize);

        /// <summary>
        /// Single equipment record with category, location, condition and balance data.
        /// </summary>
        Equipment GetByIdWithDetails(long equipmentId);

        /// <summary>
        /// Form-bound edit model for an existing record (null when missing).
        /// </summary>
        EquipmentFormViewModel GetForEdit(long equipmentId);

        /// <summary>
        /// Creates or updates an equipment record (based on EquipmentId), keeps the inventory
        /// balance and stock movement ledger in sync and writes an audit log entry.
        /// </summary>
        (bool Success, string Error, long EquipmentId) Save(EquipmentFormViewModel form);

        /// <summary>
        /// Deletes an equipment record when nothing references it.
        /// </summary>
        (bool Success, string Error) Delete(long equipmentId);

        /// <summary>
        /// Category dropdown options.
        /// </summary>
        List<LookupViewModel> GetCategoryOptions();

        /// <summary>
        /// Active location dropdown options.
        /// </summary>
        List<LookupViewModel> GetLocationOptions();

        /// <summary>
        /// Condition classification options (Good, Minor Damage, Damaged, Missing Parts, Lost).
        /// </summary>
        List<LookupViewModel> GetConditionOptions();

        /// <summary>
        /// Availability status options (Available, Under Maintenance). Shares the conditions table.
        /// </summary>
        List<LookupViewModel> GetOperationalStatusOptions();
    }
}
