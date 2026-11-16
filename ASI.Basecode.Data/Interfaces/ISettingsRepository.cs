using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.QueryModels;
using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    public interface ISettingsRepository
    {
        BorrowingPolicyVersion GetCurrentPolicy();

        List<BorrowingPolicyVersion> GetPolicyHistory();

        List<EquipmentCategory> GetCategories();

        List<Location> GetLocations();

        bool CategoryHasEquipment(long categoryId);

        bool LocationHasEquipment(long locationId);
    }
}
