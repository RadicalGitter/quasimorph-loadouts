using System;
using System.Collections.Generic;
using System.Linq;
using MGSC;
using UnityEngine;

namespace QuasimorphLoadouts
{
    internal sealed class LoadoutHotkeyController : MonoBehaviour
    {
        private static bool _hintShown;
        private ArsenalScreen _screen;
        private Mercenary _mercenary;
        private bool _busy;
        private string _selectedPresetName = PresetStore.DefaultPresetName;
        private string _draftPresetName = PresetStore.DefaultPresetName;
        private Rect _windowRect = new Rect(18f, 180f, 355f, 155f);

        internal void Configure(ArsenalScreen screen, Mercenary mercenary)
        {
            _screen = screen;
            _mercenary = mercenary;
            RefreshSelectedPreset();

            if (!_hintShown)
            {
                _hintShown = true;
                Notify("Loadouts: F5 cycles; F6 saves; F7 applies; F8 normalizes.");
            }
        }

        private void Update()
        {
            if (_busy || _screen == null || !_screen.isActiveAndEnabled || !UI.GetActiveViews().Contains(_screen))
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                CyclePreset(1);
            }
            else if (Input.GetKeyDown(KeyCode.F6))
            {
                SavePreset();
            }
            else if (Input.GetKeyDown(KeyCode.F7))
            {
                ApplyPreset();
            }
            else if (Input.GetKeyDown(KeyCode.F8))
            {
                NormalizePreset();
            }
        }

        private void OnGUI()
        {
            if (_screen == null || !_screen.isActiveAndEnabled || !UI.GetActiveViews().Contains(_screen))
            {
                return;
            }

            GUI.depth = -1000;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = !_busy;
            _windowRect = GUI.Window(731945, _windowRect, DrawPresetWindow, "Loadout Presets");
            GUI.enabled = wasEnabled;
        }

        private void DrawPresetWindow(int windowId)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(32f)))
            {
                CyclePreset(-1);
            }
            _draftPresetName = GUILayout.TextField(_draftPresetName ?? string.Empty, 32);
            if (GUILayout.Button(">", GUILayout.Width(32f)))
            {
                CyclePreset(1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save (F6)"))
            {
                SavePreset();
            }
            if (GUILayout.Button("Apply (F7)"))
            {
                ApplyPreset();
            }
            if (GUILayout.Button("Normalize (F8)"))
            {
                NormalizePreset();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Edit the name, then Save to create a preset. Drag this window by its title.");
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 24f));
        }

        private void SavePreset()
        {
            RunSafely(() =>
            {
                LoadoutPreset preset = LoadoutSnapshot.Capture(_mercenary);
                PresetStore.Save(preset, _draftPresetName);
                _selectedPresetName = preset.Name;
                _draftPresetName = preset.Name;
                ShowAlert(
                    $"Saved preset '{preset.Name}'.\n\n" +
                    $"Equipment slots: {preset.Equipment.Count}\n" +
                    $"Backpack item types: {preset.Backpack.Count}\n" +
                    $"Vest item types: {preset.Vest.Count}\n\n" +
                    PresetStore.PresetPath);
            });
        }

        private void ApplyPreset()
        {
            RunSafely(() =>
            {
                LoadoutPreset preset = PresetStore.Load(_selectedPresetName);
                if (preset == null)
                {
                    ShowAlert($"No '{_selectedPresetName}' loadout exists yet. Arrange the desired equipment and press F6 first.");
                    return;
                }

                GetSpaceState(out MagnumCargo cargo, out SpaceTime spaceTime);
                OperationResult result = LoadoutApplier.Apply(preset, _mercenary, cargo, spaceTime);
                _screen.RefreshView();
                ShowAlert(result.ToDisplayText());
            });
        }

        private void NormalizePreset()
        {
            RunSafely(() =>
            {
                LoadoutPreset preset = PresetStore.Load(_selectedPresetName);
                if (preset == null)
                {
                    ShowAlert($"No '{_selectedPresetName}' loadout exists yet. Arrange the desired equipment and press F6 first.");
                    return;
                }

                GetSpaceState(out MagnumCargo cargo, out SpaceTime spaceTime);
                OperationResult result = LoadoutApplier.Normalize(preset, _mercenary, cargo, spaceTime);
                _screen.RefreshView();
                ShowAlert(result.ToDisplayText());
            });
        }

        private void RefreshSelectedPreset()
        {
            _selectedPresetName = PresetStore.GetActivePresetName();
            _draftPresetName = _selectedPresetName;
        }

        private void CyclePreset(int direction)
        {
            RunSafely(() =>
            {
                List<string> names = PresetStore.GetPresetNames();
                if (names.Count == 0)
                {
                    _selectedPresetName = PresetStore.DefaultPresetName;
                    _draftPresetName = _selectedPresetName;
                    Notify("No presets saved yet.");
                    return;
                }

                int index = names.FindIndex(name => string.Equals(name, _selectedPresetName, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    index = 0;
                }
                else
                {
                    index = (index + direction + names.Count) % names.Count;
                }

                _selectedPresetName = names[index];
                _draftPresetName = _selectedPresetName;
                PresetStore.SetActivePreset(_selectedPresetName);
                Notify("Selected loadout: " + _selectedPresetName);
            });
        }

        private static void GetSpaceState(out MagnumCargo cargo, out SpaceTime spaceTime)
        {
            SpaceGameMode gameMode = SingletonMonoBehaviour<SpaceGameMode>.Instance;
            if (gameMode == null)
            {
                throw new InvalidOperationException("Space game state is not available.");
            }

            cargo = gameMode.Get<MagnumCargo>();
            spaceTime = gameMode.Get<SpaceTime>();
        }

        private void RunSafely(Action operation)
        {
            _busy = true;
            try
            {
                operation();
            }
            catch (Exception exception)
            {
                Debug.LogError("[QuasimorphLoadouts] Loadout operation failed.");
                Debug.LogException(exception);
                ShowAlert("Loadout operation failed safely. No further items were moved.\n\n" + exception.Message);
            }
            finally
            {
                _busy = false;
            }
        }

        private static void Notify(string text)
        {
            if (UI.Staff?.NotificationPanel != null)
            {
                UI.Staff.NotificationPanel.AddNotification(text, preventRepeats: true);
            }
        }

        private static void ShowAlert(string text)
        {
            UI.Chain<AlertDialogWindow>()
                .Invoke(window => window.Configure(text))
                .Show();
        }
    }
}
