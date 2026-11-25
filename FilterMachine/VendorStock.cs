using System;
using System.Collections.Generic;
using System.Linq;

namespace FilterMachine
{
    /// <summary>
    /// Stock the two vendors that sell water gear with the machine: the retired water
    /// merchant (a customer who visits the store to sell) and Dr. Jackson (the inventor).
    /// </summary>
    internal static class VendorStock
    {
        private const string RetiredWaterMerchantId = "retired_water_merchant";
        private const string SpecialStockEventId = "special_stock";

        // The icon is 48x48, so the item covers 3x3 cells and needs a hole that size.
        private const int MachineSide = 3;

        private static readonly Random pick = new Random();

        private static ModLog log;
        private static bool active;
        private static HashSet<GameItem> inventorTableBefore;
        private static bool inventorStockedEarly;

        public static void Enable(ModLog modLog)
        {
            log = modLog;
            active = true;
            ModHook.OnGenerateCustomerVeryLate += WrapWaterMerchant;
            ModHook.OnPlaceInventorInventoryItemEarly += RememberInventorTable;
            ModHook.OnPlaceInventorInventoryItemLate += StockInventorTable;
        }

        public static void Disable()
        {
            active = false;
            inventorTableBefore = null;
            ModHook.OnGenerateCustomerVeryLate -= WrapWaterMerchant;
            ModHook.OnPlaceInventorInventoryItemEarly -= RememberInventorTable;
            ModHook.OnPlaceInventorInventoryItemLate -= StockInventorTable;
        }

        private static void RememberInventorTable(List<GameItem> ownedItems)
        {
            // PlaceInventorSpecialStock (the special_stock afterhour event) fires the Early
            // hook but never the Late one, so there would be nothing left to stock from:
            // take the table as it is and let the machine in before the stock goes down.
            inventorStockedEarly = AfterhourEvents.IsActive(SpecialStockEventId);
            inventorTableBefore = inventorStockedEarly ? null : new HashSet<GameItem>(TableItems());

            if (inventorStockedEarly)
            {
                TryPlaceMachine();
            }
        }

        private static void StockInventorTable(List<GameItem> ownedItems)
        {
            HashSet<GameItem> before = inventorTableBefore;
            bool alreadyStocked = inventorStockedEarly;

            inventorTableBefore = null;
            inventorStockedEarly = false;

            if (alreadyStocked)
            {
                return;
            }

            if (before == null)
            {
                TryPlaceMachine();
                return;
            }

            StockMachine(before, "Dr. Jackson's table");
        }

        private static void WrapWaterMerchant(StoreClientManager manager)
        {
            if (manager == null || manager.clientStack == null)
            {
                return;
            }

            foreach (StoreClient client in manager.clientStack)
            {
                if (client == null || client.identifier != RetiredWaterMerchantId)
                {
                    continue;
                }

                Dialogue dialogue = client.mainDialogue;
                if (dialogue == null)
                {
                    continue;
                }

                // GenerateClient runs every morning and the merchant can sit in the queue for
                // a day or two, so only the first pass may wrap his dialogue.
                if (dialogue.endAction != null && dialogue.endAction.Target is WaterMerchantStock)
                {
                    continue;
                }

                dialogue.endAction = new WaterMerchantStock(dialogue.endAction).Run;
            }
        }

        private static bool TryPlaceMachine()
        {
            PlayerStore store = PlayerStore.Instance;
            return store != null && store.TryAddDirectSellingItemToTable(ModEntry.ItemId);
        }

        private static List<GameItem> TableItems()
        {
            EmporiumEntry emporium = EmporiumEntry.Instance;
            GameGridInventory front = emporium != null ? emporium.frontInvinvElement : null;
            return front != null && front.childItems != null
                ? new List<GameItem>(front.childItems)
                : new List<GameItem>();
        }

        /// <summary>
        /// Puts the machine on the table the vendor has just stocked. The counter the water
        /// gear lands on is 18x4 cells and that gear already covers almost all of it, so
        /// when the machine does not fit one of the vendor's own goods is cleared away: only
        /// an item that could hold a 3x3 block leaves a usable hole, hence the shape filter.
        /// </summary>
        private static void StockMachine(HashSet<GameItem> before, string table)
        {
            if (TryPlaceMachine())
            {
                return;
            }

            // Identify the vendor's own goods by the difference: leftover items from another
            // customer may already be sitting on the table.
            List<GameItem> replaced = TableItems()
                .Where(item => !before.Contains(item) && CanHoldMachine(item))
                .ToList();

            Shuffle(replaced);
            foreach (GameItem item in replaced)
            {
                string identifier = item.identifier;
                item.Destroy();
                if (TryPlaceMachine())
                {
                    log.Log("replaced " + identifier + " with the machine on " + table);
                    return;
                }
            }

            log.Warning("no room for the machine on " + table);
        }

        private static bool CanHoldMachine(GameItem item)
        {
            GridShape shape = item != null ? item.modifiedShape : null;
            return shape != null && shape.width >= MachineSide && shape.height >= MachineSide;
        }

        private static void Shuffle(List<GameItem> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = pick.Next(i + 1);
                GameItem swap = items[i];
                items[i] = items[j];
                items[j] = swap;
            }
        }

        private sealed class WaterMerchantStock
        {
            private readonly Action stockAction;

            public WaterMerchantStock(Action stockAction)
            {
                this.stockAction = stockAction;
            }

            public void Run()
            {
                HashSet<GameItem> before = active ? new HashSet<GameItem>(TableItems()) : null;
                Invoke();

                if (before == null)
                {
                    return;
                }

                // The end action runs inside the dialogue code, which has no guard of its own:
                // a fault here would cut the customer's visit short, so keep it contained.
                try
                {
                    StockMachine(before, "the retired water merchant's table");
                }
                catch (Exception error)
                {
                    log.Error("could not stock the retired water merchant's table: " + error);
                }
            }

            private void Invoke()
            {
                if (stockAction != null)
                {
                    stockAction();
                }
            }
        }
    }
}
