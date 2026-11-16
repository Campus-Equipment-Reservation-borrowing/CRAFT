using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using ASI.Basecode.Data.QueryModels;

namespace ASI.Basecode.Data.Repositories
{
    /// <summary>
    /// Equipment repository implementation. Queries only — business rules live in services.
    /// </summary>
    public class EquipmentRepository : BaseRepository, IEquipmentRepository
    {
        public EquipmentRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public Equipment GetByIdWithDetails(long equipmentId)
        {
            return this.Context.Equipment
                .Include(e => e.EquipmentCategory)
                .Include(e => e.Location)
                .Include(e => e.CurrentCondition)
                .Include(e => e.OperationalStatus)
                .Include(e => e.EquipmentInventoryBalance)
                .AsNoTracking()
                .FirstOrDefault(e => e.EquipmentId == equipmentId);
        }

        public Equipment GetTrackedById(long equipmentId)
        {
            return this.Context.Equipment
                .Include(e => e.EquipmentInventoryBalance)
                .FirstOrDefault(e => e.EquipmentId == equipmentId);
        }

        public PagedResult<Equipment> GetPaged(
            string search,
            long? categoryId,
            long? locationId,
            long? conditionId,
            long? operationalStatusId,
            bool? isActive,
            int page,
            int pageSize)
        {
            const int DefaultPage = 1;
            const int DefaultPageSize = 10;

            var query = this.Context.Equipment
                .Include(e => e.EquipmentCategory)
                .Include(e => e.Location)
                .Include(e => e.CurrentCondition)
                .Include(e => e.OperationalStatus)
                .Include(e => e.EquipmentInventoryBalance)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search.Trim()}%";
                query = query.Where(e =>
                    EF.Functions.Like(e.EquipmentName, pattern) ||
                    EF.Functions.Like(e.AssetCode, pattern) ||
                    (e.Description != null && EF.Functions.Like(e.Description, pattern)) ||
                    EF.Functions.Like(e.EquipmentCategory.CategoryName, pattern));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(e => e.EquipmentCategoryId == categoryId.Value);
            }

            if (locationId.HasValue)
            {
                query = query.Where(e => e.LocationId == locationId.Value);
            }

            if (conditionId.HasValue)
            {
                query = query.Where(e => e.CurrentConditionId == conditionId.Value);
            }

            if (operationalStatusId.HasValue)
            {
                query = query.Where(e => e.OperationalStatusId == operationalStatusId.Value);
            }

            if (isActive.HasValue)
            {
                query = query.Where(e => e.IsActive == isActive.Value);
            }

            var totalCount = query.Count();

            page = page < 1 ? DefaultPage : page;
            pageSize = pageSize < 1 ? DefaultPageSize : pageSize;

            var items = query
                .OrderBy(e => e.EquipmentName)
                .ThenBy(e => e.EquipmentId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<Equipment>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
        }

        public bool AssetCodeExists(string assetCode, long? excludeEquipmentId)
        {
            var code = assetCode?.Trim();
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            return this.Context.Equipment
                .Any(e => e.AssetCode == code && (!excludeEquipmentId.HasValue || e.EquipmentId != excludeEquipmentId.Value));
        }

        public bool HasTransactionReferences(long equipmentId)
        {
            var referencesBorrowing = this.Context.Borrowings.Any(b => b.EquipmentId == equipmentId);
            var referencesReservation = this.Context.Reservations.Any(r => r.EquipmentId == equipmentId);
            var referencesInspection = this.Context.ConditionInspections.Any(i => i.EquipmentId == equipmentId);

            return referencesBorrowing || referencesReservation || referencesInspection;
        }

        public EquipmentTotals GetTotals()
        {
            var recordCount = this.Context.Equipment.Count();

            var balanceTotals = this.Context.EquipmentInventoryBalances
                .AsNoTracking()
                .Select(b => new
                {
                    b.TotalQuantity,
                    b.ReservedQuantity,
                    b.ReleasedQuantity,
                    b.AwaitingInspectionQuantity,
                    b.MaintenanceQuantity,
                    b.UnreconciledQuantity,
                })
                .ToList();

            return new EquipmentTotals
            {
                RecordCount = recordCount,
                TotalUnits = balanceTotals.Sum(b => b.TotalQuantity),
                ReservedUnits = balanceTotals.Sum(b => b.ReservedQuantity),
                ReleasedUnits = balanceTotals.Sum(b => b.ReleasedQuantity),
                AwaitingInspectionUnits = balanceTotals.Sum(b => b.AwaitingInspectionQuantity),
                MaintenanceUnits = balanceTotals.Sum(b => b.MaintenanceQuantity),
                UnreconciledUnits = balanceTotals.Sum(b => b.UnreconciledQuantity),
            };
        }

        public System.Collections.Generic.List<CategoryUnitTotals> GetActiveCategoryUnitTotals()
        {
            return this.Context.Equipment
                .Where(e => e.IsActive && e.EquipmentInventoryBalance != null)
                .GroupBy(e => e.EquipmentCategory.CategoryName)
                .Select(g => new CategoryUnitTotals
                {
                    Name = g.Key,
                    Total = g.Sum(e => e.EquipmentInventoryBalance.TotalQuantity),
                    Available = g.Sum(e => e.EquipmentInventoryBalance.TotalQuantity
                        - e.EquipmentInventoryBalance.ReservedQuantity
                        - e.EquipmentInventoryBalance.ReleasedQuantity
                        - e.EquipmentInventoryBalance.AwaitingInspectionQuantity
                        - e.EquipmentInventoryBalance.MaintenanceQuantity
                        - e.EquipmentInventoryBalance.UnreconciledQuantity),
                })
                .ToList();
        }

        public int CountCategories()
        {
            return this.Context.EquipmentCategories.Count();
        }

        public System.Collections.Generic.List<Equipment> GetActiveWithStockState()
        {
            return this.Context.Equipment
                .AsNoTracking()
                .Include(e => e.EquipmentInventoryBalance)
                .Include(e => e.OperationalStatus)
                .Include(e => e.CurrentCondition)
                .Where(e => e.IsActive)
                .ToList();
        }

        public void Add(Equipment equipment)
        {
            this.Context.Equipment.Add(equipment);
        }

        public void Remove(Equipment equipment)
        {
            this.Context.Equipment.Remove(equipment);
        }

        public System.Collections.Generic.List<EquipmentCategory> GetCategoriesByName()
        {
            return this.Context.EquipmentCategories
                .AsNoTracking()
                .OrderBy(c => c.CategoryName)
                .ToList();
        }

        public System.Collections.Generic.List<Location> GetActiveLocationsByName()
        {
            return this.Context.Locations
                .AsNoTracking()
                .Where(l => l.IsActive)
                .OrderBy(l => l.LocationName)
                .ToList();
        }
    }
}
