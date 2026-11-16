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

        public int? GetCurrentOverdueGracePeriodMinutes()
        {
            return this.Context.BorrowingPolicyVersions.AsNoTracking()
                .OrderByDescending(p => p.IsCurrent)
                .ThenByDescending(p => p.PolicyVersionId)
                .Select(p => (int?)p.OverdueGracePeriodMinutes)
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

        public int? GetCurrentLowStockThreshold()
        {
            return this.Context.BorrowingPolicyVersions
                .AsNoTracking()
                .Where(p => p.IsCurrent)
                .OrderByDescending(p => p.EffectiveFromUtc)
                .Select(p => (int?)p.LowStockThreshold)
                .FirstOrDefault();
        }

        public void AddPolicy(BorrowingPolicyVersion policy)
        {
            this.Context.BorrowingPolicyVersions.Add(policy);
        }

        public bool CategoryCodeExists(string upperCode)
        {
            return this.Context.EquipmentCategories.Any(c => c.CategoryCode.ToUpper() == upperCode);
        }

        public bool CategoryNameExists(string upperName)
        {
            return this.Context.EquipmentCategories.Any(c => c.CategoryName.ToUpper() == upperName);
        }

        public void AddCategory(EquipmentCategory category)
        {
            this.Context.EquipmentCategories.Add(category);
        }

        public EquipmentCategory GetTrackedCategory(long categoryId)
        {
            return this.Context.EquipmentCategories.FirstOrDefault(c => c.EquipmentCategoryId == categoryId);
        }

        public int CountEquipmentInCategory(long categoryId)
        {
            return this.Context.Equipment.Count(e => e.EquipmentCategoryId == categoryId);
        }

        public void RemoveCategory(EquipmentCategory category)
        {
            this.Context.EquipmentCategories.Remove(category);
        }

        public bool LocationCodeExists(string upperCode)
        {
            return this.Context.Locations.Any(l => l.LocationCode.ToUpper() == upperCode);
        }

        public void AddLocation(Location location)
        {
            this.Context.Locations.Add(location);
        }

        public Location GetTrackedLocation(long locationId)
        {
            return this.Context.Locations.FirstOrDefault(l => l.LocationId == locationId);
        }

        public void RemoveLocation(Location location)
        {
            this.Context.Locations.Remove(location);
        }

        public void MarkUnchanged(object entity)
        {
            this.SetEntityState(entity, EntityState.Unchanged);
        }
    }
}
