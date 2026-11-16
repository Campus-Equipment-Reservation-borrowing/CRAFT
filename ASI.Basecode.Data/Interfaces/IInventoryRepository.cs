using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Data access for equipment inventory balances and the inventory movement ledger.
    /// Adds and removes are saved by the caller.
    /// </summary>
    public interface IInventoryRepository
    {
        /// <summary>The balance row of the equipment (tracked), or null.</summary>
        EquipmentInventoryBalance GetTrackedBalance(long equipmentId);

        void AddBalance(EquipmentInventoryBalance balance);

        void RemoveBalance(EquipmentInventoryBalance balance);

        void AddMovement(InventoryMovement movement);

        /// <summary>Loads and removes every movement row of the equipment.</summary>
        void RemoveMovementsForEquipment(long equipmentId);
    }
}
