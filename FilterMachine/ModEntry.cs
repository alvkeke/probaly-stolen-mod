using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FilterMachine
{
    public class ModEntry : IMod
    {
        public const string ModId = "filter_machine";
        public const string ItemId = "filter_machine";

        private const int RackWidth = 6;
        private const int RackHeight = 6;
        private const int IterationLimit = 200000;

        private static readonly HashSet<string> RemovedLast = new HashSet<string>
        {
            "mineral",
            "chemical_contaminant",
            "heavy_metal"
        };

        private ModLog log;

        public void Init(ModManifest manifest)
        {
            log = new ModLog(manifest);
            log.Log("init");
        }

        public void OnEnable()
        {
            ModHook.OnModItemDirectoryInit += RegisterItems;
            VendorStock.Enable(log);
            ModHelper.AddDebugFunction(ModId, ModHelper.DEBUG_CATEGORY_SPAWN, "Spawn Filter Machine", SpawnMachine, tall: true);
        }

        public void OnDisable()
        {
            ModHook.OnModItemDirectoryInit -= RegisterItems;
            VendorStock.Disable();
            ModHelper.RemoveDebugFunctions(ModId);
        }

        private static void SpawnMachine()
        {
            // Ownership is what lets the player open the machine's content window.
            GameItem machine = ModHelper.SetItemOwnerShip(ModHelper.SpawnItem(ItemId), true);
            ModHelper.MoveItemToInventory(machine);
        }

        private static void RegisterItems(ModItemDirectory directory)
        {
            directory.Add(ItemId, CreateMachine);
        }

        private static GameItem CreateMachine()
        {
            GameGridInventory rack = new GameGridInventory(RackWidth, RackHeight);
            // Accept spent cartridges too: the slot predicate also runs when a save is restored,
            // and a spent cartridge must not vanish from the rack on load. uv_filter carries the
            // WATER_FILTER tag but is a drag-on tool with no durability, so keep it out the same
            // way the game's own water pitcher does.
            rack.mayInventoryAddItemFunc = (item, _) =>
                IsWaterFilter(item) && item.identifier != "uv_filter" && GeneralHelper.IsItemOwned(item);

            GameSlotInventory waterSlot = new GameSlotInventory();
            waterSlot.mayInventoryAddItemFunc = (item, _) =>
                item != null && item.IsTag(WaterHelper.LIQUID_CONTAINER_TAG) && GeneralHelper.IsItemOwned(item);
            waterSlot.SetBackgroundFadeSprite("Items/water_container4", "xl");

            MachineryButton startButton = new MachineryButton(L("fm_button_start", "Start"), null);
            MachineryProgressBar purityBar = new MachineryProgressBar(0f);
            MachineryText status = MachineryText.Status(L("fm_label_status", "Status"), L("fm_status_idle", "Idle"));

            PixelWindow window = MachineHelper.CreateMachineryWindow(
                true,
                MachineHelper.Panel(L("fm_panel_filters", "Filter Rack"), rack, "filters"),
                MachineHelper.Panel(L("fm_panel_water", "Water"), waterSlot, "water"),
                MachineHelper.Panel(
                    L("fm_panel_control", "Control"),
                    MachineryStack.Vertical(startButton, purityBar, status),
                    "control"));

            GameItem machine = ItemDirectory.CreateEmptyItem(ItemId)
                .SetContentWindow(window)
                .SetSpriteAndShapeFromMod(ModId, "filter_machine");

            machine.name = L("fm_name", "Filter Machine");
            machine.shortDescription = L("fm_desc", "");
            machine.flavorText = L("fm_flavor", "");
            machine.unitValue = 600L;
            machine.EnableTag("CONTAINER_TAG");
            machine.EnableTag("manufacture");

            MachineryHelper.InitMachinery(machine, 0);

            startButton.SetCallback(() => RunPass(machine, rack, waterSlot, purityBar, status));
            return machine;
        }

        private static void RunPass(
            GameItem machine,
            GameGridInventory rack,
            GameSlotInventory waterSlot,
            MachineryProgressBar purityBar,
            MachineryText status)
        {
            GameItem container = waterSlot.childItem;
            if (container == null || WaterHelper.IsEmpty(container))
            {
                SetStatus(status, "fm_status_no_water", "Load a water container");
                return;
            }

            List<GameItem> filters = rack.childItems.Where(IsUsableFilter).ToList();
            if (filters.Count == 0)
            {
                SetStatus(status, "fm_status_no_filters", "Load filters");
                return;
            }

            long removed = FilterWater(container, filters);
            int rawPurity = WaterHelper.GetWaterPurity(container);
            purityBar.SetValue(Mathf.Clamp01(rawPurity / 10000f));

            string purityText = (rawPurity / 100.0).ToString("0.#");
            string volumeText = (removed / 1000.0).ToString("0.#");

            if (removed > 0)
            {
                SetStatus(status, "fm_status_filtered", "Purity {0}% - removed {1} mL", purityText, volumeText);
                MachineHelper.OnMachineActioned(machine, rack);
            }
            else
            {
                SetStatus(status, "fm_status_clean", "Purity {0}% - nothing to filter", purityText);
            }
        }

        /// <returns>Total contaminant volume (in uL) converted into water.</returns>
        private static long FilterWater(GameItem container, List<GameItem> filters)
        {
            Liquid[] liquids = Liquid.Liquids.ToArray();
            if (liquids.Length == 0)
            {
                return 0;
            }

            int totalVolume = WaterHelper.GetTotalVolume(container);
            string waterPart = Liquid.GetCurrentPartFromId("water");
            int waterVolume = container.GetTagReadonly(waterPart).GetInt();

            // Pouring through a pitcher stops at the purifier's ceiling with no module
            // bonuses: purity (water over everything left) never passes the cap, so this
            // much contamination has to stay behind. It also bounds the loop. A container
            // without water keeps nothing behind, so its dirt can be filtered out too.
            int cap = Math.Max(1, MachinePurifier.GetMaxPurityCap(0));
            long keep = (long)waterVolume * (10000 - cap) / cap;
            long budget = Math.Max(0L, (long)totalVolume - waterVolume - keep);

            HashSet<string> blocked = new HashSet<string>();
            FilterRotation rotation = new FilterRotation(filters);
            long removedTotal = 0L;

            for (int guard = 0; guard < IterationLimit && budget > 0; guard++)
            {
                Liquid target = PickContaminant(container, liquids, blocked);
                if (target == null)
                {
                    break;
                }

                GameItem filter = rotation.Next();
                if (filter == null)
                {
                    break;
                }

                int current = container.GetTagReadonly(target.currentPart).GetInt();
                if (current <= 0)
                {
                    continue;
                }

                int step = Math.Max(1, target.filterVolumeRemoved);
                int chunk = (int)Math.Min(Math.Min(current, step), budget);
                int leftover = WaterHelper.HandleFilter(target.identifier, chunk, container, filter);
                int removed = chunk - leftover;

                // HandleFilter hands back the amount untouched when this filter cannot take
                // the contaminant; remember it and move on instead of stopping the pass.
                if (removed <= 0)
                {
                    blocked.Add(target.identifier);
                    continue;
                }

                int credited = TakeContaminant(container, target.identifier, removed);
                if (credited <= 0)
                {
                    blocked.Add(target.identifier);
                    continue;
                }

                removedTotal += credited;
                budget -= credited;
            }

            return removedTotal;
        }

        private static Liquid PickContaminant(GameItem container, Liquid[] liquids, HashSet<string> blocked)
        {
            Liquid best = null;
            int bestKey = int.MaxValue;

            foreach (Liquid liquid in liquids)
            {
                if (liquid.identifier == "water" || !liquid.canBeRemovedByPurifier)
                {
                    continue;
                }
                if (blocked.Contains(liquid.identifier))
                {
                    continue;
                }
                if (container.GetTagReadonly(liquid.currentPart).GetInt() <= 0)
                {
                    continue;
                }

                int key = (RemovedLast.Contains(liquid.identifier) ? 1000000 : 0) - liquid.particleSize;
                if (key < bestKey)
                {
                    bestKey = key;
                    best = liquid;
                }
            }

            return best;
        }

        /// <summary>
        /// Hands out the cartridges of one pass one turn at a time, in the order they sit in
        /// the rack, so every filter loses the same durability instead of the rack burning
        /// down one cartridge at a time. A cartridge at zero drops out of the rotation.
        /// </summary>
        private sealed class FilterRotation
        {
            private readonly List<GameItem> filters;
            private int cursor;

            public FilterRotation(List<GameItem> filters)
            {
                this.filters = filters;
            }

            public GameItem Next()
            {
                for (int i = 0; i < filters.Count; i++)
                {
                    int index = (cursor + i) % filters.Count;
                    if (DurabilityHelper.GetRealDurability(filters[index]) > 0)
                    {
                        cursor = (index + 1) % filters.Count;
                        return filters[index];
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Takes the contaminant out of the container. What the cartridge traps leaves with
        /// it, exactly like pouring through the vanilla pitcher, so nothing is converted back
        /// into liquid and a container holding acid or another non-water liquid works too.
        /// </summary>
        private static int TakeContaminant(GameItem container, string liquidId, int amount)
        {
            string part = Liquid.GetCurrentPartFromId(liquidId);
            int current = container.GetTagReadonly(part).GetInt();
            int removed = Math.Min(current, amount);
            if (removed <= 0)
            {
                return 0;
            }

            int remaining = current - removed;
            container.ModifyTag(part, state => state.Enable().SetInt(remaining));
            WaterFeatureHelper.UpdateWaterFeature(container, false);
            return removed;
        }

        private static bool IsWaterFilter(GameItem item)
        {
            return item != null && item.IsTag("WATER_FILTER");
        }

        private static bool IsUsableFilter(GameItem item)
        {
            return IsWaterFilter(item) && DurabilityHelper.GetRealDurability(item) > 0;
        }

        private static void SetStatus(MachineryText status, string key, string fallback, params object[] args)
        {
            status.SetStatus(L("fm_label_status", "Status"), L(key, fallback, args));
        }

        private static string L(string key, string fallback, params object[] args)
        {
            string text = args == null || args.Length == 0
                ? ModHelper.GetLocalized(ModId, key)
                : ModHelper.GetLocalized(ModId, key, args);

            return string.IsNullOrEmpty(text) || text == key ? fallback : text;
        }
    }
}
