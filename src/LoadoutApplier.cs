using System;
using System.Collections.Generic;
using System.Linq;
using MGSC;

namespace QuasimorphLoadouts
{
    internal static class LoadoutApplier
    {
        internal static OperationResult Apply(LoadoutPreset preset, Mercenary mercenary, MagnumCargo cargo, SpaceTime spaceTime)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (mercenary?.CreatureData?.Inventory == null) throw new ArgumentException("No mercenary inventory is available.", nameof(mercenary));
            if (cargo == null) throw new ArgumentNullException(nameof(cargo));
            if (spaceTime == null) throw new ArgumentNullException(nameof(spaceTime));

            OperationResult result = new OperationResult();
            Inventory inventory = mercenary.CreatureData.Inventory;

            ApplyEquipment(preset, inventory, cargo, spaceTime, result);
            ApplyContents("backpack", preset.Backpack, inventory.BackpackStore, cargo, spaceTime, result);
            ApplyContents("vest", preset.Vest, inventory.VestStore, cargo, spaceTime, result);
            return result;
        }

        private static void ApplyEquipment(
            LoadoutPreset preset,
            Inventory inventory,
            MagnumCargo cargo,
            SpaceTime spaceTime,
            OperationResult result)
        {
            foreach (EquipmentPreset desired in preset.Equipment ?? new List<EquipmentPreset>())
            {
                LoadoutSlot definition = LoadoutSlots.Find(desired.Slot);
                if (definition == null)
                {
                    result.Problems.Add($"Unknown preset slot: {desired.Slot}");
                    continue;
                }

                ItemStorage target = definition.Storage(inventory);
                if (target == null || target.Width * target.Height == 0)
                {
                    if (!string.IsNullOrEmpty(desired.ItemId))
                    {
                        result.Problems.Add($"{definition.Id} is unavailable for this mercenary");
                    }
                    continue;
                }

                BasePickupItem current = target.First;
                if (string.Equals(current?.Id, desired.ItemId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(desired.ItemId))
                {
                    if (current != null && ReturnToCargo(current, cargo, spaceTime))
                    {
                        result.EquipmentChanged++;
                    }
                    else if (current != null)
                    {
                        result.Problems.Add($"Could not clear {definition.Id}");
                    }
                    continue;
                }

                BasePickupItem replacement = FindBestCargoItem(cargo, desired.ItemId);
                if (replacement == null)
                {
                    result.Problems.Add($"Missing {ItemName(desired.ItemId)} for {definition.Id}");
                    continue;
                }

                if (!target.IsValidItem(replacement))
                {
                    result.Problems.Add($"{ItemName(desired.ItemId)} is not valid in {definition.Id}");
                    continue;
                }

                if (current != null && !ReturnToCargo(current, cargo, spaceTime))
                {
                    result.Problems.Add($"Could not safely unload {ItemName(current.Id)} from {definition.Id}");
                    continue;
                }

                if (target.TryPutItem(replacement, CellPosition.Zero))
                {
                    result.EquipmentChanged++;
                    continue;
                }

                if (current != null && target.Empty)
                {
                    target.TryPutItem(current, CellPosition.Zero);
                }
                result.Problems.Add($"Could not equip {ItemName(desired.ItemId)} in {definition.Id}");
            }
        }

        private static void ApplyContents(
            string containerName,
            List<ItemQuantityPreset> desiredItems,
            ItemStorage target,
            MagnumCargo cargo,
            SpaceTime spaceTime,
            OperationResult result)
        {
            if (target == null)
            {
                if (desiredItems != null && desiredItems.Any(item => item.Quantity > 0))
                {
                    result.Problems.Add($"No {containerName} storage is available");
                }
                return;
            }

            foreach (ItemQuantityPreset desired in desiredItems ?? new List<ItemQuantityPreset>())
            {
                if (string.IsNullOrEmpty(desired.ItemId) || desired.Quantity <= 0)
                {
                    continue;
                }

                int before = target.CountItems(desired.ItemId);
                int needed = Math.Max(0, desired.Quantity - before);
                if (needed == 0)
                {
                    continue;
                }

                TransferQuantity(desired.ItemId, needed, target, cargo, spaceTime);
                int moved = Math.Max(0, target.CountItems(desired.ItemId) - before);
                result.QuantityMoved += moved;

                int remaining = Math.Max(0, desired.Quantity - target.CountItems(desired.ItemId));
                if (remaining > 0)
                {
                    int available = CountCargo(cargo, desired.ItemId);
                    string reason = available == 0 ? "missing from cargo" : $"not enough {containerName} space ({available} still in cargo)";
                    result.Problems.Add($"{remaining}× {ItemName(desired.ItemId)}: {reason}");
                }
            }
        }

        private static void TransferQuantity(
            string itemId,
            int requested,
            ItemStorage target,
            MagnumCargo cargo,
            SpaceTime spaceTime)
        {
            int remaining = requested;
            while (remaining > 0)
            {
                BasePickupItem source = FindBestCargoItem(cargo, itemId);
                if (source == null)
                {
                    return;
                }

                int before = target.CountItems(itemId);
                if (source.StackCount <= remaining)
                {
                    AddItemToStorage(source, target, spaceTime);
                }
                else
                {
                    SplitAndAdd(source, remaining, target, cargo, spaceTime);
                }

                int moved = Math.Max(0, target.CountItems(itemId) - before);
                if (moved == 0)
                {
                    return;
                }
                remaining -= moved;
            }
        }

        private static void SplitAndAdd(
            BasePickupItem source,
            int requested,
            ItemStorage target,
            MagnumCargo cargo,
            SpaceTime spaceTime)
        {
            short splitCount = (short)Math.Min(requested, source.StackCount - 1);
            if (splitCount <= 0)
            {
                return;
            }

            ItemStorage originalStorage = source.Storage;
            BasePickupItem split = SingletonMonoBehaviour<ItemFactory>.Instance.CreateForInventory(source.Id);
            if (split == null)
            {
                return;
            }

            source.StackCount -= splitCount;
            split.StackCount = splitCount;
            split.ExaminedItem = source.ExaminedItem;
            split.CopyExpireTime(source);
            ItemInteractionSystem.TrySplitUsable(source, split);

            int before = target.CountItems(source.Id);
            AddItemToStorage(split, target, spaceTime);
            int accepted = Math.Max(0, target.CountItems(source.Id) - before);
            if (accepted < splitCount && split.StackCount > 0 && split.Storage == null)
            {
                MagnumCargoSystem.AddCargo(cargo, spaceTime, split, originalStorage, splittedItem: true);
            }
        }

        private static void AddItemToStorage(BasePickupItem item, ItemStorage target, SpaceTime spaceTime)
        {
            if (item == null)
            {
                return;
            }

            bool emptyAfterMerge;
            ItemInteractionSystem.TryMergeIntoStorage(target, item, spaceTime, out emptyAfterMerge);
            if (!emptyAfterMerge && item.StackCount > 0)
            {
                target.TryPutItem(item, CellPosition.Zero);
            }
        }

        private static bool ReturnToCargo(BasePickupItem item, MagnumCargo cargo, SpaceTime spaceTime)
        {
            if (item == null || item.Storage == null)
            {
                return false;
            }

            ItemStorage original = item.Storage;
            MagnumCargoSystem.AddCargo(cargo, spaceTime, item, cargo.ShipCargo[0]);
            return item.Storage != null && item.Storage != original && IsCargoStorage(cargo, item.Storage);
        }

        private static BasePickupItem FindBestCargoItem(MagnumCargo cargo, string itemId)
        {
            return CargoStorages(cargo)
                .SelectMany(storage => storage.Items)
                .Where(item => item != null && string.Equals(item.Id, itemId, StringComparison.Ordinal))
                .OrderByDescending(ConditionScore)
                .FirstOrDefault();
        }

        private static float ConditionScore(BasePickupItem item)
        {
            BreakableItemComponent breakable = item.Comp<BreakableItemComponent>();
            return breakable?.CurrentPercent ?? 1f;
        }

        private static int CountCargo(MagnumCargo cargo, string itemId)
        {
            return CargoStorages(cargo).Sum(storage => storage.CountItems(itemId));
        }

        private static IEnumerable<ItemStorage> CargoStorages(MagnumCargo cargo)
        {
            foreach (ItemStorage storage in cargo.ShipCargo)
            {
                yield return storage;
            }

            if (cargo.FridgeStorage != null)
            {
                yield return cargo.FridgeStorage;
            }
        }

        private static bool IsCargoStorage(MagnumCargo cargo, ItemStorage storage)
        {
            return cargo.ShipCargo.Contains(storage) || storage == cargo.FridgeStorage;
        }

        private static string ItemName(string itemId)
        {
            try
            {
                string localized = Localization.Get("item." + itemId + ".name");
                return string.IsNullOrEmpty(localized) ? itemId : localized;
            }
            catch
            {
                return itemId;
            }
        }
    }
}
