using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using ErrorMessages = ASI.Basecode.Resources.Messages.Errors;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Equipment administration service: validation, inventory ledger sync and audit logging.
    /// Data access stays in the repository; this layer owns the business rules.
    /// </summary>
    public class EquipmentService : ServiceBase, IEquipmentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEquipmentRepository _equipmentRepository;
        private readonly ICurrentUserService _currentUser;
        private readonly IMapper _mapper;

        /// <summary>Direct EF access for the small codebook/ledger writes this service owns.</summary>
        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public EquipmentService(
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            IEquipmentRepository equipmentRepository,
            ICurrentUserService currentUser,
            IMapper mapper)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
            this._equipmentRepository = equipmentRepository;
            this._currentUser = currentUser;
            this._mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public EquipmentIndexViewModel GetIndexViewModel(
            string search,
            long? categoryId,
            long? locationId,
            long? conditionId,
            long? operationalStatusId,
            int page,
            int pageSize)
        {
            var result = this._equipmentRepository.GetPaged(
                search, categoryId, locationId, conditionId, operationalStatusId, isActive: null, page, pageSize);

            return new EquipmentIndexViewModel
            {
                Page = new PagedResult<EquipmentListViewModel>
                {
                    Items = result.Items.Select(this.MapToListItem).ToList(),
                    Page = result.Page,
                    PageSize = result.PageSize,
                    TotalCount = result.TotalCount,
                },
                Totals = this._equipmentRepository.GetTotals(),
                Search = search,
                CategoryId = categoryId,
                LocationId = locationId,
                ConditionId = conditionId,
                OperationalStatusId = operationalStatusId,
            };
        }

        public Equipment GetByIdWithDetails(long equipmentId)
        {
            return this._equipmentRepository.GetByIdWithDetails(equipmentId);
        }

        public EquipmentFormViewModel GetForEdit(long equipmentId)
        {
            var entity = this._equipmentRepository.GetByIdWithDetails(equipmentId);
            if (entity == null)
            {
                return null;
            }

            var form = this._mapper.Map<EquipmentFormViewModel>(entity);
            form.TotalQuantity = entity.EquipmentInventoryBalance?.TotalQuantity ?? 0;
            return form;
        }

        public (bool Success, string Error, long EquipmentId) Save(EquipmentFormViewModel form)
        {
            try
            {
                if (form == null)
                {
                    return (false, ErrorMessages.ServerError, 0);
                }

                form.EquipmentName = form.EquipmentName?.Trim();
                form.AssetCode = form.AssetCode?.Trim();

                if (this._equipmentRepository.AssetCodeExists(form.AssetCode, form.EquipmentId))
                {
                    return (false, ErrorMessages.EquipmentAssetCodeDuplicate, form.EquipmentId);
                }

                return form.EquipmentId > 0
                    ? this.Update(form)
                    : this.Create(form);
            }
            catch (DbUpdateConcurrencyException)
            {
                this._logger.LogWarning("Concurrency conflict while saving equipment {EquipmentId}.", form?.EquipmentId);
                return (false, ErrorMessages.EquipmentUpdateConflict, form?.EquipmentId ?? 0);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to save equipment {EquipmentId}.", form?.EquipmentId);
                return (false, ErrorMessages.ServerError, form?.EquipmentId ?? 0);
            }
        }

        public (bool Success, string Error) Delete(long equipmentId)
        {
            try
            {
                var entity = this._equipmentRepository.GetTrackedById(equipmentId);
                if (entity == null)
                {
                    return (false, ErrorMessages.EquipmentNotFound);
                }

                if (this._equipmentRepository.HasTransactionReferences(equipmentId))
                {
                    return (false, ErrorMessages.EquipmentDeleteBlocked);
                }

                // Remove the balance row and movement ledger explicitly so the
                // reverse-engineered ClientSetNull FK behaviour never bites us.
                var movements = this.DbContext.InventoryMovements.Where(m => m.EquipmentId == equipmentId).ToList();
                this.DbContext.InventoryMovements.RemoveRange(movements);

                var balance = this.DbContext.EquipmentInventoryBalances.FirstOrDefault(b => b.EquipmentId == equipmentId);
                if (balance != null)
                {
                    this.DbContext.EquipmentInventoryBalances.Remove(balance);
                }

                this.DbContext.Equipment.Remove(entity);
                this._unitOfWork.SaveChanges();

                this.WriteAudit("DELETE", "Equipment", entity.EquipmentId, entity.AssetCode,
                    $"Deleted equipment '{entity.EquipmentName}' ({entity.AssetCode}).");

                return (true, null);
            }
            catch (DbUpdateConcurrencyException)
            {
                this._logger.LogWarning("Concurrency conflict while deleting equipment {EquipmentId}.", equipmentId);
                return (false, ErrorMessages.EquipmentUpdateConflict);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to delete equipment {EquipmentId}.", equipmentId);
                return (false, ErrorMessages.ServerError);
            }
        }

        public List<LookupViewModel> GetCategoryOptions()
        {
            return this.DbContext.EquipmentCategories
                .AsNoTracking()
                .OrderBy(c => c.CategoryName)
                .Select(c => new LookupViewModel { Id = c.EquipmentCategoryId, Text = c.CategoryName })
                .ToList();
        }

        public List<LookupViewModel> GetLocationOptions()
        {
            return this.DbContext.Locations
                .AsNoTracking()
                .Where(l => l.IsActive)
                .OrderBy(l => l.LocationName)
                .Select(l => new LookupViewModel { Id = l.LocationId, Text = l.LocationName })
                .ToList();
        }

        public List<LookupViewModel> GetConditionOptions()
        {
            return this.DbContext.Conditions
                .AsNoTracking()
                .OrderBy(c => c.Code)
                .ThenBy(c => c.Description)
                .Select(c => new LookupViewModel { Id = c.ConditionId, Text = c.Description })
                .ToList();
        }

        private (bool Success, string Error, long EquipmentId) Create(EquipmentFormViewModel form)
        {
            var entity = this._mapper.Map<Equipment>(form);
            entity.EquipmentId = 0; // ignore any posted id on create
            entity.CreatedAtUtc = default; // let the DB default (sysutcdatetime) fill it

            this.DbContext.Equipment.Add(entity);

            this.DbContext.EquipmentInventoryBalances.Add(new EquipmentInventoryBalance
            {
                Equipment = entity,
                TotalQuantity = form.TotalQuantity,
                ReservedQuantity = 0,
                ReleasedQuantity = 0,
                AwaitingInspectionQuantity = 0,
                MaintenanceQuantity = 0,
                UnreconciledQuantity = 0,
            });

            this.DbContext.InventoryMovements.Add(new InventoryMovement
            {
                Equipment = entity,
                MovementType = "INITIAL_STOCK",
                ToState = "AVAILABLE",
                Quantity = form.TotalQuantity,
                ReferenceNumber = "EQ-INIT",
                PerformedByUserId = this._currentUser.GetActorUserId(),
            });

            this._unitOfWork.SaveChanges();

            this.WriteAudit("CREATE", "Equipment", entity.EquipmentId, entity.AssetCode,
                $"Created equipment '{entity.EquipmentName}' ({entity.AssetCode}) with {form.TotalQuantity} unit(s).");

            return (true, null, entity.EquipmentId);
        }

        private (bool Success, string Error, long EquipmentId) Update(EquipmentFormViewModel form)
        {
            var entity = this._equipmentRepository.GetTrackedById(form.EquipmentId);
            if (entity == null)
            {
                return (false, ErrorMessages.EquipmentNotFound, form.EquipmentId);
            }

            var balance = entity.EquipmentInventoryBalance
                ?? this.DbContext.EquipmentInventoryBalances.FirstOrDefault(b => b.EquipmentId == entity.EquipmentId);

            if (balance == null)
            {
                balance = new EquipmentInventoryBalance { EquipmentId = entity.EquipmentId };
                this.DbContext.EquipmentInventoryBalances.Add(balance);
            }

            var committed = balance.ReservedQuantity + balance.ReleasedQuantity + balance.AwaitingInspectionQuantity
                + balance.MaintenanceQuantity + balance.UnreconciledQuantity;
            if (form.TotalQuantity < committed)
            {
                return (false, ErrorMessages.EquipmentQuantityBelowCommitted, form.EquipmentId);
            }

            var delta = form.TotalQuantity - balance.TotalQuantity;

            this._mapper.Map(form, entity);
            entity.EquipmentId = form.EquipmentId;

            if (delta != 0)
            {
                balance.TotalQuantity = form.TotalQuantity;
                this.DbContext.InventoryMovements.Add(new InventoryMovement
                {
                    EquipmentId = entity.EquipmentId,
                    MovementType = delta > 0 ? "RESTOCK" : "STOCK_ADJUSTMENT",
                    Quantity = Math.Abs(delta),
                    ReferenceNumber = "EQ-EDIT",
                    PerformedByUserId = this._currentUser.GetActorUserId(),
                });
            }

            this._unitOfWork.SaveChanges();

            this.WriteAudit("UPDATE", "Equipment", entity.EquipmentId, entity.AssetCode,
                $"Updated equipment '{entity.EquipmentName}' ({entity.AssetCode})" +
                (delta != 0
                    ? $", total quantity {form.TotalQuantity} ({(delta > 0 ? "+" : string.Empty)}{delta})."
                    : "."));

            return (true, null, entity.EquipmentId);
        }

        private EquipmentListViewModel MapToListItem(Equipment entity)
        {
            var item = this._mapper.Map<EquipmentListViewModel>(entity);
            item.TotalQuantity = entity.EquipmentInventoryBalance?.TotalQuantity ?? 0;
            item.AvailableUnits = item.TotalQuantity
                - (entity.EquipmentInventoryBalance?.ReservedQuantity ?? 0)
                - (entity.EquipmentInventoryBalance?.ReleasedQuantity ?? 0)
                - (entity.EquipmentInventoryBalance?.AwaitingInspectionQuantity ?? 0)
                - (entity.EquipmentInventoryBalance?.MaintenanceQuantity ?? 0)
                - (entity.EquipmentInventoryBalance?.UnreconciledQuantity ?? 0);
            return item;
        }

        private void WriteAudit(string actionType, string entityType, long entityId, string referenceNumber, string details)
        {
            this.DbContext.AuditLogs.Add(new AuditLog
            {
                ActorUserId = this._currentUser.GetActorUserId(),
                ActionType = actionType,
                EntityType = entityType,
                EntityId = entityId,
                ReferenceNumber = referenceNumber,
                Details = details,
                OccurredAtUtc = DateTime.UtcNow,
            });
            this._unitOfWork.SaveChanges();
        }
    }
}
