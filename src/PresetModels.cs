using System.Collections.Generic;

namespace QuasimorphLoadouts
{
    public sealed class PresetFile
    {
        public int SchemaVersion { get; set; } = 3;
        public string ActivePreset { get; set; } = "Default";
        public List<LoadoutPreset> Presets { get; set; } = new List<LoadoutPreset>();
    }

    public sealed class LoadoutPreset
    {
        public string Name { get; set; } = "Default";
        public string IconItemId { get; set; }
        public string GameVersion { get; set; }
        public string SavedAtUtc { get; set; }
        public List<EquipmentPreset> Equipment { get; set; } = new List<EquipmentPreset>();
        public List<ItemQuantityPreset> Backpack { get; set; } = new List<ItemQuantityPreset>();
        public List<ItemQuantityPreset> Vest { get; set; } = new List<ItemQuantityPreset>();
    }

    public sealed class EquipmentPreset
    {
        public string Slot { get; set; }
        public string ItemId { get; set; }
    }

    public sealed class ItemQuantityPreset
    {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public int? PreferredX { get; set; }
        public int? PreferredY { get; set; }
    }
}
