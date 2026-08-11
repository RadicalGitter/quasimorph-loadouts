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

        internal static List<LoadoutPreset> GetPresets()
        {
            return LoadFileOrNew().Presets
                .OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
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

        internal static void UpdateMetadata(string originalName, string newName, string iconItemId)
        {
            originalName = ValidateName(originalName);
            newName = ValidateName(newName);
            PresetFile file = LoadFileOrNew();
            LoadoutPreset preset = file.Presets.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, originalName, StringComparison.OrdinalIgnoreCase));
            if (preset == null)
            {
                throw new InvalidOperationException($"Loadout '{originalName}' no longer exists.");
            }

            bool renamed = !string.Equals(originalName, newName, StringComparison.OrdinalIgnoreCase);
            if (renamed && file.Presets.Any(candidate =>
                    string.Equals(candidate.Name, newName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A loadout named '{newName}' already exists.");
            }

            preset.Name = newName;
            preset.IconItemId = string.IsNullOrWhiteSpace(iconItemId) ? null : iconItemId;
            file.ActivePreset = newName;
            file.Presets = file.Presets.OrderBy(candidate => candidate.Name, StringComparer.OrdinalIgnoreCase).ToList();
            SaveFile(file);
        }

        private static PresetFile LoadFileOrNew()
        {
            if (!File.Exists(PresetPath))
            {
                return new PresetFile();
            }

            PresetFile file = JsonConvert.DeserializeObject<PresetFile>(File.ReadAllText(PresetPath));
            if (file == null || file.SchemaVersion < 1 || file.SchemaVersion > 3)
            {
                throw new InvalidDataException("Unsupported or empty loadout preset file.");
            }

            // Older schemas omit nullable coordinates and/or the icon item ID. Those fields
            // deserialize cleanly, so migration is intentionally lossless.
            file.SchemaVersion = 3;

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
