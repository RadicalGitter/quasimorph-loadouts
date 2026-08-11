using System;
using System.Collections.Generic;
using System.Linq;
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

            return storage.Items
                .Where(item => item != null && !string.IsNullOrEmpty(item.Id))
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .Select(group => new ItemQuantityPreset
                {
                    ItemId = group.Key,
                    Quantity = group.Sum(item => (int)item.StackCount)
                })
                .OrderBy(item => item.ItemId, StringComparer.Ordinal)
                .ToList();
        }
    }
}

