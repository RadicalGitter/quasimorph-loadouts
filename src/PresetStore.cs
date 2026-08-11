using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace QuasimorphLoadouts
{
    internal static class PresetStore
    {
        internal const string DefaultPresetName = "Default";

        internal static string PresetDirectory => Path.GetFullPath(
            Path.Combine(Application.persistentDataPath, "..", "Quasimorph_ModConfigs", "QuasimorphLoadouts"));

        internal static string PresetPath => Path.Combine(PresetDirectory, "presets.json");

        internal static void SaveDefault(LoadoutPreset preset)
        {
            Directory.CreateDirectory(PresetDirectory);
            PresetFile file = LoadFileOrNew();
            file.ActivePreset = DefaultPresetName;
            file.Presets.RemoveAll(candidate => string.Equals(candidate.Name, DefaultPresetName, StringComparison.OrdinalIgnoreCase));
            file.Presets.Add(preset);

            string temporaryPath = PresetPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(file, Formatting.Indented));

            if (File.Exists(PresetPath))
            {
                File.Replace(temporaryPath, PresetPath, null);
            }
            else
            {
                File.Move(temporaryPath, PresetPath);
            }
        }

        internal static LoadoutPreset LoadDefault()
        {
            PresetFile file = LoadFileOrNew();
            return file.Presets.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, DefaultPresetName, StringComparison.OrdinalIgnoreCase));
        }

        private static PresetFile LoadFileOrNew()
        {
            if (!File.Exists(PresetPath))
            {
                return new PresetFile();
            }

            PresetFile file = JsonConvert.DeserializeObject<PresetFile>(File.ReadAllText(PresetPath));
            if (file == null || file.SchemaVersion != 1)
            {
                throw new InvalidDataException("Unsupported or empty loadout preset file.");
            }

            if (file.Presets == null)
            {
                file.Presets = new System.Collections.Generic.List<LoadoutPreset>();
            }

            return file;
        }
    }
}
