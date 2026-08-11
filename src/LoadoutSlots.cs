using System;
using System.Collections.Generic;
using MGSC;

namespace QuasimorphLoadouts
{
    internal sealed class LoadoutSlot
    {
        internal LoadoutSlot(string id, Func<Inventory, ItemStorage> storage)
        {
            Id = id;
            Storage = storage;
        }

        internal string Id { get; }
        internal Func<Inventory, ItemStorage> Storage { get; }
    }

    internal static class LoadoutSlots
    {
        internal static readonly IReadOnlyList<LoadoutSlot> All = new List<LoadoutSlot>
        {
            new LoadoutSlot("PrimaryWeapon", inventory => inventory.PrimarySlot),
            new LoadoutSlot("SecondaryWeapon", inventory => inventory.SecondarySlot),
            new LoadoutSlot("AdditionalWeapon", inventory => inventory.AdditionalSlot),
            new LoadoutSlot("ServoArmWeapon", inventory => inventory.ServoArmSlot),
            new LoadoutSlot("Backpack", inventory => inventory.BackpackSlot),
            new LoadoutSlot("Vest", inventory => inventory.VestSlot),
            new LoadoutSlot("Armor", inventory => inventory.ArmorSlot),
            new LoadoutSlot("Helmet", inventory => inventory.HelmetSlot),
            new LoadoutSlot("Leggings", inventory => inventory.LeggingsSlot),
            new LoadoutSlot("Boots", inventory => inventory.BootsSlot)
        };

        internal static LoadoutSlot Find(string id)
        {
            foreach (LoadoutSlot slot in All)
            {
                if (string.Equals(slot.Id, id, StringComparison.Ordinal))
                {
                    return slot;
                }
            }

            return null;
        }
    }
}

