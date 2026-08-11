using System;
using System.Collections.Generic;
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
        internal static string BackupPath => PresetPath + ".bak";

        internal static void Save(LoadoutPreset preset, string name)
        {
            name = ValidateName(name);
            Directory.CreateDirectory(PresetDirectory);
            PresetFile file = LoadFileOrNew();
            preset.Name = name;
            file.ActivePreset = name;
            file.Presets.RemoveAll(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
            file.Presets.Add(preset);
            file.Presets = file.Presets.OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase).ToList();
            SaveFile(file);
        }

        internal static LoadoutPreset Load(string name)
        {
            PresetFile file = LoadFileOrNew();
            return file.Presets.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        internal static List<string> GetPresetNames()
        {
            return LoadFileOrNew().Presets
                .Select(preset => preset.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static string GetActivePresetName()
        {
            PresetFile file = LoadFileOrNew();
            if (file.Presets.Any(preset => string.Equals(preset.Name, file.ActivePreset, StringComparison.OrdinalIgnoreCase)))
            {
                return file.ActivePreset;
            }
            return file.Presets.FirstOrDefault()?.Name ?? DefaultPresetName;
        }

        internal static void SetActivePreset(string name)
        {
            PresetFile file = LoadFileOrNew();
            if (!file.Presets.Any(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            file.ActivePreset = name;
            SaveFile(file);
        }

        private static PresetFile LoadFileOrNew()
        {
            if (!File.Exists(PresetPath))
            {
                return new PresetFile();
            }

            PresetFile file = JsonConvert.DeserializeObject<PresetFile>(File.ReadAllText(PresetPath));
            if (file == null || file.SchemaVersion < 1 || file.SchemaVersion > 2)
            {
                throw new InvalidDataException("Unsupported or empty loadout preset file.");
            }

            // Schema 1 stored aggregate quantities without position hints. Missing nullable
            // coordinates deserialize cleanly, so migration is intentionally lossless.
            file.SchemaVersion = 2;

            if (file.Presets == null)
            {
                file.Presets = new System.Collections.Generic.List<LoadoutPreset>();
            }

            return file;
        }

        private static string ValidateName(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                throw new ArgumentException("Preset name cannot be empty.");
            }
            if (name.Length > 32)
            {
                throw new ArgumentException("Preset name must be 32 characters or fewer.");
            }
            if (name.Any(char.IsControl))
            {
                throw new ArgumentException("Preset name contains an unsupported character.");
            }
            return name;
        }

        private static void SaveFile(PresetFile file)
        {
            Directory.CreateDirectory(PresetDirectory);
            string temporaryPath = PresetPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(file, Formatting.Indented));

            if (File.Exists(PresetPath))
            {
                File.Copy(PresetPath, BackupPath, overwrite: true);
                File.Replace(temporaryPath, PresetPath, null);
            }
            else
            {
                File.Move(temporaryPath, PresetPath);
            }
        }
    }
}
