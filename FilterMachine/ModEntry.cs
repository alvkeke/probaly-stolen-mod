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
            ModHelper.AddDebugFunction(ModId, ModHelper.DEBUG_CATEGORY_SPAWN, "Spawn Filter Machine", SpawnMachine, tall: true);
        }

        public void OnDisable()
        {
            ModHook.OnModItemDirectoryInit -= RegisterItems;
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

            // Same ceiling the built-in purifier applies with no module bonuses: a pass never
            // strips the last fraction of contamination, which also bounds the loop.
            long floor = (long)totalVolume * (10000 - MachinePurifier.GetMaxPurityCap(0)) / 10000;
            long budget = Math.Max(0L, (long)totalVolume - waterVolume - floor);

            HashSet<string> blocked = new HashSet<string>();
            long removedTotal = 0L;

            for (int guard = 0; guard < IterationLimit && budget > 0; guard++)
            {
                Liquid target = PickContaminant(container, liquids, blocked);
                if (target == null)
                {
                    break;
                }

                GameItem filter = PickFreshestFilter(filters);
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

                int credited = RemoveAndCreditWater(container, target.identifier, removed);
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
        /// Always spends the freshest filter, so wear evens out across the whole rack
        /// instead of burning down one cartridge at a time.
        /// </summary>
        private static GameItem PickFreshestFilter(List<GameItem> filters)
        {
            GameItem best = null;
            int bestDurability = 0;

            foreach (GameItem filter in filters)
            {
                int durability = DurabilityHelper.GetRealDurability(filter);
                if (durability > bestDurability)
                {
                    bestDurability = durability;
                    best = filter;
                }
            }

            return best;
        }

        private static int RemoveAndCreditWater(GameItem container, string liquidId, int amount)
        {
            int removed = WaterHelper.RemoveContaminantFromContainer(container, liquidId, amount, 0);
            if (removed <= 0)
            {
                return 0;
            }

            // The game's purifier turns filtered contaminants into clean water; do the same so
            // the container keeps its volume instead of quietly losing liquid.
            string waterPart = Liquid.GetCurrentPartFromId("water");
            int water = container.GetTagReadonly(waterPart).GetInt();
            container.ModifyTag(waterPart, state => state.Enable().SetInt(water + removed));
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
