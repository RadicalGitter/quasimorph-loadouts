using System;
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

        internal void Configure(ArsenalScreen screen, Mercenary mercenary)
        {
            _screen = screen;
            _mercenary = mercenary;

            if (!_hintShown)
            {
                _hintShown = true;
                Notify("Loadouts: F6 saves Default; F7 applies Default.");
            }
        }

        private void Update()
        {
            if (_busy || _screen == null || !_screen.isActiveAndEnabled || !UI.GetActiveViews().Contains(_screen))
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F6))
            {
                SavePreset();
            }
            else if (Input.GetKeyDown(KeyCode.F7))
            {
                ApplyPreset();
            }
        }

        private void SavePreset()
        {
            RunSafely(() =>
            {
                LoadoutPreset preset = LoadoutSnapshot.Capture(_mercenary);
                PresetStore.SaveDefault(preset);
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
                LoadoutPreset preset = PresetStore.LoadDefault();
                if (preset == null)
                {
                    ShowAlert("No Default loadout exists yet. Open this screen with the desired equipment and press F6 first.");
                    return;
                }

                SpaceGameMode gameMode = SingletonMonoBehaviour<SpaceGameMode>.Instance;
                if (gameMode == null)
                {
                    throw new InvalidOperationException("Space game state is not available.");
                }

                MagnumCargo cargo = gameMode.Get<MagnumCargo>();
                SpaceTime spaceTime = gameMode.Get<SpaceTime>();
                OperationResult result = LoadoutApplier.Apply(preset, _mercenary, cargo, spaceTime);
                _screen.RefreshView();
                ShowAlert(result.ToDisplayText());
            });
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

