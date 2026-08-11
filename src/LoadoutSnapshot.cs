using System;
using System.Collections.Generic;
using MGSC;
using UnityEngine;

namespace QuasimorphLoadouts
{
    internal static class LoadoutSnapshot
    {
        internal static LoadoutPreset Capture(Mercenary mercenary)
        {
            if (mercenary?.CreatureData?.Inventory == null)
            {
                throw new ArgumentException("No mercenary inventory is available.", nameof(mercenary));
            }

            Inventory inventory = mercenary.CreatureData.Inventory;
            LoadoutPreset preset = new LoadoutPreset
            {
                Name = PresetStore.DefaultPresetName,
                GameVersion = Application.version,
                SavedAtUtc = DateTime.UtcNow.ToString("O")
            };

            foreach (LoadoutSlot slot in LoadoutSlots.All)
            {
                ItemStorage storage = slot.Storage(inventory);
                preset.Equipment.Add(new EquipmentPreset
                {
                    Slot = slot.Id,
                    ItemId = storage?.First?.Id
                });
            }

            preset.Backpack = CaptureQuantities(inventory.BackpackStore);
            preset.Vest = CaptureQuantities(inventory.VestStore);
            return preset;
        }

        private static List<ItemQuantityPreset> CaptureQuantities(ItemStorage storage)
        {
            if (storage == null)
            {
                return new List<ItemQuantityPreset>();
            }

            List<ItemQuantityPreset> result = new List<ItemQuantityPreset>();
            foreach (BasePickupItem item in storage.Items)
            {
                if (item == null || string.IsNullOrEmpty(item.Id))
                {
                    continue;
                }

                result.Add(new ItemQuantityPreset
                {
                    ItemId = item.Id,
                    Quantity = item.StackCount,
                    PreferredX = item.InventoryPos.X,
                    PreferredY = item.InventoryPos.Y
                });
            }

            result.Sort((left, right) =>
            {
                int byRow = Nullable.Compare(left.PreferredY, right.PreferredY);
                return byRow != 0 ? byRow : Nullable.Compare(left.PreferredX, right.PreferredX);
            });
            return result;
        }
    }
}
