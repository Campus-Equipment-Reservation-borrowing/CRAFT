using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class SettingsRepository : BaseRepository, ISettingsRepository
    {
        public SettingsRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public BorrowingPolicyVersion GetCurrentPolicy()
        {
            return this.Context.BorrowingPolicyVersions
                .OrderByDescending(p => p.IsCurrent)
                .ThenByDescending(p => p.PolicyVersionId)
                .FirstOrDefault();
        }

        public List<BorrowingPolicyVersion> GetPolicyHistory()
        {
            return this.Context.BorrowingPolicyVersions
                .OrderByDescending(p => p.EffectiveFromUtc)
                .ToList();
        }

        public List<EquipmentCategory> GetCategories()
        {
            return this.Context.EquipmentCategories.OrderBy(c => c.CategoryName).ToList();
        }

        public List<Location> GetLocations()
        {
            return this.Context.Locations.OrderBy(l => l.LocationName).ToList();
        }

        public bool CategoryHasEquipment(long categoryId)
        {
            return this.Context.Equipment.Any(e => e.EquipmentCategoryId == categoryId);
        }

        public bool LocationHasEquipment(long locationId)
        {
            return this.Context.Equipment.Any(e => e.LocationId == locationId);
        }
    }
}
