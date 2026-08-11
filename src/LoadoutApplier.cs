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
            ValidateArguments(preset, mercenary, cargo, spaceTime);

            OperationResult result = new OperationResult();
            Inventory inventory = mercenary.CreatureData.Inventory;

            ApplyEquipment(preset, inventory, cargo, spaceTime, result);
            ApplyContents("backpack", preset.Backpack, inventory.BackpackStore, cargo, spaceTime, result);
            ApplyContents("vest", preset.Vest, inventory.VestStore, cargo, spaceTime, result);
            return result;
        }

        internal static OperationResult Normalize(LoadoutPreset preset, Mercenary mercenary, MagnumCargo cargo, SpaceTime spaceTime)
        {
            ValidateArguments(preset, mercenary, cargo, spaceTime);

            OperationResult result = new OperationResult();
            Inventory inventory = mercenary.CreatureData.Inventory;

            // Equipment first because changing a backpack or vest can change container shape.
            ApplyEquipment(preset, inventory, cargo, spaceTime, result);
            NormalizeContents("backpack", preset.Backpack, inventory.BackpackStore, cargo, spaceTime, result);
            NormalizeContents("vest", preset.Vest, inventory.VestStore, cargo, spaceTime, result);
            ApplyContents("backpack", preset.Backpack, inventory.BackpackStore, cargo, spaceTime, result);
            ApplyContents("vest", preset.Vest, inventory.VestStore, cargo, spaceTime, result);
            return result;
        }

        private static void ValidateArguments(LoadoutPreset preset, Mercenary mercenary, MagnumCargo cargo, SpaceTime spaceTime)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            if (mercenary?.CreatureData?.Inventory == null) throw new ArgumentException("No mercenary inventory is available.", nameof(mercenary));
            if (cargo == null) throw new ArgumentNullException(nameof(cargo));
            if (spaceTime == null) throw new ArgumentNullException(nameof(spaceTime));
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

            IEnumerable<IGrouping<string, ItemQuantityPreset>> desiredGroups =
                (desiredItems ?? new List<ItemQuantityPreset>())
                .Where(item => !string.IsNullOrEmpty(item.ItemId) && item.Quantity > 0)
                .GroupBy(item => item.ItemId, StringComparer.Ordinal);

            foreach (IGrouping<string, ItemQuantityPreset> desiredGroup in desiredGroups)
            {
                string itemId = desiredGroup.Key;
                int desiredQuantity = desiredGroup.Sum(item => item.Quantity);
                List<ItemQuantityPreset> placements = desiredGroup.ToList();

                int before = target.CountItems(itemId);
                int needed = Math.Max(0, desiredQuantity - before);
                if (needed == 0)
                {
                    continue;
                }

                TransferQuantity(itemId, needed, placements, target, cargo, spaceTime);
                int moved = Math.Max(0, target.CountItems(itemId) - before);
                result.QuantityMoved += moved;

                int remaining = Math.Max(0, desiredQuantity - target.CountItems(itemId));
                if (remaining > 0)
                {
                    int available = CountCargo(cargo, itemId);
                    string reason = available == 0 ? "missing from cargo" : $"not enough {containerName} space ({available} still in cargo)";
                    result.Problems.Add($"{remaining}× {ItemName(itemId)}: {reason}");
                }
            }
        }

        private static void NormalizeContents(
            string containerName,
            List<ItemQuantityPreset> desiredItems,
            ItemStorage target,
            MagnumCargo cargo,
            SpaceTime spaceTime,
            OperationResult result)
        {
            if (target == null)
            {
                return;
            }

            Dictionary<string, int> desiredCounts = (desiredItems ?? new List<ItemQuantityPreset>())
                .Where(item => !string.IsNullOrEmpty(item.ItemId) && item.Quantity > 0)
                .GroupBy(item => item.ItemId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity), StringComparer.Ordinal);
            Dictionary<string, int> keptCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (BasePickupItem item in target.Items.ToList())
            {
                int desiredCount;
                desiredCounts.TryGetValue(item.Id, out desiredCount);
                int alreadyKept;
                keptCounts.TryGetValue(item.Id, out alreadyKept);
                int keepCount = Math.Min(item.StackCount, Math.Max(0, desiredCount - alreadyKept));
                int excessCount = item.StackCount - keepCount;

                if (excessCount == 0)
                {
                    keptCounts[item.Id] = alreadyKept + item.StackCount;
                    continue;
                }

                if (item.Locked)
                {
                    keptCounts[item.Id] = alreadyKept + item.StackCount;
                    result.Problems.Add($"Locked {ItemName(item.Id)} could not be unloaded from {containerName}");
                    continue;
                }

                if (keepCount == 0)
                {
                    short quantity = item.StackCount;
                    if (ReturnToCargo(item, cargo, spaceTime))
                    {
                        result.QuantityUnloaded += quantity;
                    }
                    else
                    {
                        result.Problems.Add($"Could not safely unload {ItemName(item.Id)} from {containerName}");
                    }
                    continue;
                }

                if (SplitExcessToCargo(item, (short)excessCount, cargo, spaceTime))
                {
                    result.QuantityUnloaded += excessCount;
                    keptCounts[item.Id] = alreadyKept + keepCount;
                }
                else
                {
                    keptCounts[item.Id] = alreadyKept + item.StackCount;
                    result.Problems.Add($"Could not safely split excess {ItemName(item.Id)} in {containerName}");
                }
            }
        }

        private static void TransferQuantity(
            string itemId,
            int requested,
            List<ItemQuantityPreset> placements,
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
                    AddItemToStorage(source, placements, target, spaceTime);
                }
                else
                {
                    SplitAndAdd(source, remaining, placements, target, cargo, spaceTime);
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
            List<ItemQuantityPreset> placements,
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
            AddItemToStorage(split, placements, target, spaceTime);
            int accepted = Math.Max(0, target.CountItems(source.Id) - before);
            if (accepted < splitCount && split.StackCount > 0 && split.Storage == null)
            {
                MagnumCargoSystem.AddCargo(cargo, spaceTime, split, originalStorage, splittedItem: true);
            }
        }

        private static void AddItemToStorage(
            BasePickupItem item,
            List<ItemQuantityPreset> placements,
            ItemStorage target,
            SpaceTime spaceTime)
        {
            if (item == null)
            {
                return;
            }

            bool emptyAfterMerge;
            ItemInteractionSystem.TryMergeIntoStorage(target, item, spaceTime, out emptyAfterMerge);
            if (!emptyAfterMerge && item.StackCount > 0)
            {
                bool placed = false;
                foreach (ItemQuantityPreset placement in placements ?? new List<ItemQuantityPreset>())
                {
                    if (placement.PreferredX.HasValue && placement.PreferredY.HasValue &&
                        target.TryPutItem(
                            item,
                            new CellPosition(placement.PreferredX.Value, placement.PreferredY.Value),
                            hasSpecialPos: true))
                    {
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    target.TryPutItem(item, CellPosition.Zero);
                }
            }
        }

        private static bool SplitExcessToCargo(
            BasePickupItem source,
            short excessCount,
            MagnumCargo cargo,
            SpaceTime spaceTime)
        {
            if (source == null || source.Storage == null || excessCount <= 0 || excessCount >= source.StackCount)
            {
                return false;
            }

            BasePickupItem split = SingletonMonoBehaviour<ItemFactory>.Instance.CreateForInventory(source.Id);
            if (split == null)
            {
                return false;
            }

            int cargoBefore = CountCargo(cargo, source.Id);
            source.StackCount -= excessCount;
            split.StackCount = excessCount;
            split.ExaminedItem = source.ExaminedItem;
            split.CopyExpireTime(source);
            ItemInteractionSystem.TrySplitUsable(source, split);
            MagnumCargoSystem.AddCargo(cargo, spaceTime, split, cargo.ShipCargo[0], splittedItem: true);
            return CountCargo(cargo, source.Id) >= cargoBefore + excessCount;
        }

        private static bool ReturnToCargo(BasePickupItem item, MagnumCargo cargo, SpaceTime spaceTime)
        {
            if (item == null || item.Storage == null)
            {
                return false;
            }

            int cargoBefore = CountCargo(cargo, item.Id);
            short quantity = item.StackCount;
            MagnumCargoSystem.AddCargo(cargo, spaceTime, item, cargo.ShipCargo[0]);
            return CountCargo(cargo, item.Id) >= cargoBefore + quantity;
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
