using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class InventoryRepository : BaseRepository, IInventoryRepository
    {
        public InventoryRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public EquipmentInventoryBalance GetTrackedBalance(long equipmentId)
        {
            return this.Context.EquipmentInventoryBalances.FirstOrDefault(b => b.EquipmentId == equipmentId);
        }

        public void AddBalance(EquipmentInventoryBalance balance)
        {
            this.Context.EquipmentInventoryBalances.Add(balance);
        }

        public void RemoveBalance(EquipmentInventoryBalance balance)
        {
            this.Context.EquipmentInventoryBalances.Remove(balance);
        }

        public void AddMovement(InventoryMovement movement)
        {
            this.Context.InventoryMovements.Add(movement);
        }

        public void RemoveMovementsForEquipment(long equipmentId)
        {
            var movements = this.Context.InventoryMovements.Where(m => m.EquipmentId == equipmentId).ToList();
            this.Context.InventoryMovements.RemoveRange(movements);
        }

        public int TryHold(long equipmentId, int quantity)
        {
            return this.Context.EquipmentInventoryBalances
                .Where(b => b.EquipmentId == equipmentId
                    && b.TotalQuantity - b.ReservedQuantity - b.ReleasedQuantity - b.AwaitingInspectionQuantity
                        - b.MaintenanceQuantity - b.UnreconciledQuantity >= quantity)
                .ExecuteUpdate(u => u.SetProperty(b => b.ReservedQuantity, b => b.ReservedQuantity + quantity));
        }
    }
}
