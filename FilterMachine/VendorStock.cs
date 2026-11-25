using System.Collections.Generic;

namespace FilterMachine
{
    /// <summary>
    /// Puts the machine on the sale table Dr. Jackson stocks when the player visits him or
    /// when he comes to the store. The game exposes the Early tier for exactly this: it runs
    /// before his own catalogue, so the machine is on the table before the counter gets
    /// tight and his surplus is what the game drops.
    /// </summary>
    internal static class VendorStock
    {
        private static ModLog log;

        public static void Enable(ModLog modLog)
        {
            log = modLog;
            ModHook.OnPlaceInventorInventoryItemEarly += StockInventorTable;
        }

        public static void Disable()
        {
            ModHook.OnPlaceInventorInventoryItemEarly -= StockInventorTable;
        }

        private static void StockInventorTable(List<GameItem> ownedItems)
        {
            PlayerStore store = PlayerStore.Instance;
            if (store == null || !store.TryAddDirectSellingItemToTable(ModEntry.ItemId))
            {
                log.Warning("Dr. Jackson's table had no room for the machine");
            }
        }
    }
}
