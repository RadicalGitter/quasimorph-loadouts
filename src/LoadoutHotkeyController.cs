using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace QuasimorphLoadouts
{
    internal sealed class LoadoutHotkeyController : MonoBehaviour
    {
        internal static bool IsModalEditorOpen { get; private set; }

        private const float SlotWidth = 196f;
        private const float SlotHeight = 94f;
        private const float AddSlotWidth = 52f;
        private const float SlotGap = 8f;
        private const float HeaderOffset = 72f;
        private const int EditorWindowId = 731946;

        private static readonly FieldInfo InventoryWindowField =
            AccessTools.Field(typeof(ArsenalScreen), "_inventoryWindow");
        private static bool _hintShown;

        private ArsenalScreen _screen;
        private Mercenary _mercenary;
        private bool _busy;
        private string _selectedPresetName = PresetStore.DefaultPresetName;
        private List<LoadoutPreset> _presets = new List<LoadoutPreset>();
        private int _pageOffset;
        private string _hoveredPresetName;

        private bool _editorOpen;
        private bool _editorCreatesPreset;
        private Rect _editorRect = new Rect(0f, 0f, 420f, 330f);
        private string _editorOriginalName;
        private string _editorName;
        private string _editorIconItemId;
        private string _editorError;
        private Vector2 _editorScroll;
        private List<string> _editorIconCandidates = new List<string>();
        private LoadoutPreset _pendingNewPreset;

        internal void Configure(ArsenalScreen screen, Mercenary mercenary)
        {
            _screen = screen;
            _mercenary = mercenary;
            RefreshPresetCache();

            if (!_hintShown)
            {
                _hintShown = true;
                Notify("Loadouts: click an icon to apply; hover it to replace or edit. F8 normalizes.");
            }
        }

        private void Update()
        {
            if (_busy || !IsArsenalActive())
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                CyclePreset(1);
            }
            else if (Input.GetKeyDown(KeyCode.F6))
            {
                ReplacePresetWithCurrent(_selectedPresetName);
            }
            else if (Input.GetKeyDown(KeyCode.F7))
            {
                ApplyPreset(_selectedPresetName);
            }
            else if (Input.GetKeyDown(KeyCode.F8))
            {
                NormalizePreset();
            }
        }

        private void OnGUI()
        {
            if (!IsArsenalActive())
            {
                return;
            }

            GUI.depth = -1000;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = !_busy && !_editorOpen;
            DrawPresetStrip(GetPresetStripRect());
            GUI.enabled = wasEnabled;

            if (_editorOpen)
            {
                _editorRect = GUI.ModalWindow(EditorWindowId, _editorRect, DrawEditorWindow, "Edit Loadout");
            }
        }

        private bool IsArsenalActive()
        {
            return _screen != null
                && _screen.isActiveAndEnabled
                && UI.GetActiveViews().Contains(_screen);
        }

        private Rect GetPresetStripRect()
        {
            Rect fallback = new Rect(18f, 8f, Mathf.Min(1250f, Screen.width - 36f), SlotHeight);
            try
            {
                GameObject inventoryWindow = InventoryWindowField?.GetValue(_screen) as GameObject;
                RectTransform rectTransform = inventoryWindow?.transform as RectTransform;
                if (rectTransform == null)
                {
                    return fallback;
                }

                Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
                Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                Vector2 lowerLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                Vector2 upperRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                float left = Mathf.Min(lowerLeft.x, upperRight.x);
                float right = Mathf.Max(lowerLeft.x, upperRight.x);
                float top = Screen.height - Mathf.Max(lowerLeft.y, upperRight.y);
                float width = Mathf.Clamp(right - left, 210f, Screen.width - 8f);
                return new Rect(
                    Mathf.Clamp(left, 4f, Screen.width - width - 4f),
                    Mathf.Max(4f, top - SlotHeight - HeaderOffset),
                    width,
                    SlotHeight);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[QuasimorphLoadouts] Using fallback preset-strip position: " + exception.Message);
                return fallback;
            }
        }

        private void DrawPresetStrip(Rect stripRect)
        {
            const float pagerWidth = 28f;
            int unpagedCapacity = Math.Max(1, Mathf.FloorToInt(
                (stripRect.width - AddSlotWidth - SlotGap) / (SlotWidth + SlotGap)));
            bool needsPaging = _presets.Count > unpagedCapacity;
            float pagingWidth = needsPaging ? (pagerWidth + SlotGap) * 2f : 0f;
            int capacity = Math.Max(1, Mathf.FloorToInt(
                (stripRect.width - AddSlotWidth - SlotGap - pagingWidth) / (SlotWidth + SlotGap)));
            int maximumOffset = Math.Max(0, _presets.Count - capacity);
            _pageOffset = Mathf.Clamp(_pageOffset, 0, maximumOffset);
            int visibleCount = Math.Min(capacity, Math.Max(0, _presets.Count - _pageOffset));
            float groupWidth = visibleCount * SlotWidth
                + Math.Max(0, visibleCount - 1) * SlotGap
                + (visibleCount > 0 ? SlotGap : 0f)
                + AddSlotWidth
                + pagingWidth;
            float groupStart = stripRect.center.x - groupWidth / 2f;
            float presetStart = groupStart + (needsPaging ? pagerWidth + SlotGap : 0f);

            if (needsPaging)
            {
                Rect previousRect = new Rect(groupStart, stripRect.y, pagerWidth, SlotHeight);
                if (GUI.Button(previousRect, "<"))
                {
                    _pageOffset = Math.Max(0, _pageOffset - 1);
                }
            }

            Vector2 mouse = Event.current.mousePosition;
            string slotUnderMouse = null;
            Rect hoveredSlotRect = default(Rect);
            Rect existingHoveredRect = default(Rect);
            bool existingHoverVisible = false;

            for (int visibleIndex = 0; visibleIndex < capacity; visibleIndex++)
            {
                int presetIndex = _pageOffset + visibleIndex;
                if (presetIndex >= _presets.Count)
                {
                    break;
                }

                LoadoutPreset preset = _presets[presetIndex];
                Rect slotRect = new Rect(
                    presetStart + visibleIndex * (SlotWidth + SlotGap),
                    stripRect.y,
                    SlotWidth,
                    SlotHeight);
                if (string.Equals(preset.Name, _hoveredPresetName, StringComparison.OrdinalIgnoreCase))
                {
                    existingHoveredRect = slotRect;
                    existingHoverVisible = true;
                }
                if (slotRect.Contains(mouse))
                {
                    slotUnderMouse = preset.Name;
                    hoveredSlotRect = slotRect;
                }

                DrawPresetSlot(preset, slotRect);
            }

            float addX = presetStart + visibleCount * (SlotWidth + SlotGap);
            Rect addRect = new Rect(addX, stripRect.y, AddSlotWidth, SlotHeight);
            DrawFramedSlot(addRect, selected: false);
            GUI.Label(addRect, "+", GetCenteredLabelStyle());
            if (GUI.Button(addRect, new GUIContent(string.Empty, "Save current inventory as a new loadout"), GUIStyle.none))
            {
                BeginCreateEditor();
            }
            if (needsPaging)
            {
                Rect nextRect = new Rect(addRect.xMax + SlotGap, stripRect.y, pagerWidth, SlotHeight);
                if (GUI.Button(nextRect, ">"))
                {
                    _pageOffset = Math.Min(maximumOffset, _pageOffset + 1);
                }
            }

            if (slotUnderMouse != null)
            {
                _hoveredPresetName = slotUnderMouse;
                existingHoveredRect = hoveredSlotRect;
                existingHoverVisible = true;
            }

            if (existingHoverVisible)
            {
                Rect popupRect = GetHoverPopupRect(existingHoveredRect);
                if (!existingHoveredRect.Contains(mouse) && !popupRect.Contains(mouse) && slotUnderMouse == null)
                {
                    _hoveredPresetName = null;
                }
                else
                {
                    DrawHoverActions(_hoveredPresetName, popupRect);
                }
            }
            else
            {
                _hoveredPresetName = null;
            }
        }

        private void DrawPresetSlot(LoadoutPreset preset, Rect rect)
        {
            bool selected = string.Equals(preset.Name, _selectedPresetName, StringComparison.OrdinalIgnoreCase);
            DrawFramedSlot(rect, selected);
            Sprite icon = LoadoutIconResolver.Resolve(GetEffectiveIconItemId(preset));
            if (icon != null)
            {
                LoadoutIconResolver.Draw(icon, new Rect(rect.x + 8f, rect.y + 7f, rect.width - 16f, rect.height - 14f));
            }
            else
            {
                string fallback = string.IsNullOrEmpty(preset.Name) ? "?" : preset.Name.Substring(0, 1).ToUpperInvariant();
                GUI.Label(rect, fallback, GetCenteredLabelStyle());
            }

            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            if (clicked)
            {
                SelectAndApplyPreset(preset.Name);
            }
        }

        private static void DrawFramedSlot(Rect rect, bool selected)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.018f, 0.035f, 0.045f, 0.96f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            GUI.color = selected
                ? new Color(1f, 0.93f, 0.08f, 1f)
                : new Color(0.38f, 0.86f, 0.67f, 1f);
            const float border = 3f;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, border), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - border, rect.width, border), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, border, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - border, rect.y, border, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static Rect GetHoverPopupRect(Rect slotRect)
        {
            const float width = 260f;
            const float height = 58f;
            float x = Mathf.Clamp(slotRect.center.x - width / 2f, 4f, Screen.width - width - 4f);
            return new Rect(x, Mathf.Max(2f, slotRect.y - height), width, height);
        }

        private void DrawHoverActions(string presetName, Rect popupRect)
        {
            GUI.Box(popupRect, GUIContent.none);
            GUI.Label(new Rect(popupRect.x + 6f, popupRect.y + 3f, popupRect.width - 12f, 20f), presetName, GetCenteredLabelStyle());
            Rect replaceRect = new Rect(popupRect.x + 5f, popupRect.y + 26f, 143f, 27f);
            Rect editRect = new Rect(replaceRect.xMax + 4f, replaceRect.y, popupRect.width - 14f - replaceRect.width, 27f);
            if (GUI.Button(replaceRect, "Replace with Current"))
            {
                ReplacePresetWithCurrent(presetName);
            }
            if (GUI.Button(editRect, "Edit Loadout"))
            {
                BeginEditEditor(presetName);
            }
        }

        private void BeginCreateEditor()
        {
            try
            {
                _pendingNewPreset = LoadoutSnapshot.Capture(_mercenary);
                _editorCreatesPreset = true;
                _editorOriginalName = null;
                _editorName = SuggestNewPresetName();
                _editorIconCandidates = GetIconCandidates(_pendingNewPreset);
                _editorIconItemId = GetEffectiveIconItemId(_pendingNewPreset);
                OpenEditor();
            }
            catch (Exception exception)
            {
                HandleFailure(exception);
            }
        }

        private void BeginEditEditor(string presetName)
        {
            LoadoutPreset preset = FindPreset(presetName);
            if (preset == null)
            {
                ShowAlert($"Loadout '{presetName}' no longer exists.");
                return;
            }

            _pendingNewPreset = null;
            _editorCreatesPreset = false;
            _editorOriginalName = preset.Name;
            _editorName = preset.Name;
            _editorIconCandidates = GetIconCandidates(preset);
            _editorIconItemId = GetEffectiveIconItemId(preset);
            OpenEditor();
        }

        private void OpenEditor()
        {
            _editorError = null;
            _editorScroll = Vector2.zero;
            _editorRect.x = Mathf.Max(4f, (Screen.width - _editorRect.width) / 2f);
            _editorRect.y = Mathf.Max(4f, (Screen.height - _editorRect.height) / 2f);
            _editorOpen = true;
            IsModalEditorOpen = true;
        }

        private void DrawEditorWindow(int windowId)
        {
            GUI.Label(new Rect(12f, 25f, 55f, 22f), "Name");
            _editorName = GUI.TextField(new Rect(68f, 24f, _editorRect.width - 80f, 24f), _editorName ?? string.Empty, 32);
            GUI.Label(new Rect(12f, 53f, _editorRect.width - 24f, 20f), "Icon (from items saved in this loadout)");

            Rect gridRect = new Rect(12f, 75f, _editorRect.width - 24f, 190f);
            GUI.Box(gridRect, GUIContent.none);
            const float iconSize = 48f;
            const float gap = 5f;
            int columns = Math.Max(1, Mathf.FloorToInt((gridRect.width - 14f) / (iconSize + gap)));
            int rows = Math.Max(1, Mathf.CeilToInt(_editorIconCandidates.Count / (float)columns));
            float contentHeight = Math.Max(gridRect.height - 4f, rows * (iconSize + gap) + 4f);
            Rect viewRect = new Rect(0f, 0f, gridRect.width - 18f, contentHeight);
            _editorScroll = GUI.BeginScrollView(new Rect(gridRect.x + 2f, gridRect.y + 2f, gridRect.width - 4f, gridRect.height - 4f), _editorScroll, viewRect);

            for (int index = 0; index < _editorIconCandidates.Count; index++)
            {
                string itemId = _editorIconCandidates[index];
                int column = index % columns;
                int row = index / columns;
                Rect iconRect = new Rect(3f + column * (iconSize + gap), 3f + row * (iconSize + gap), iconSize, iconSize);
                Color originalBackground = GUI.backgroundColor;
                if (string.Equals(itemId, _editorIconItemId, StringComparison.Ordinal))
                {
                    GUI.backgroundColor = new Color(0.95f, 0.72f, 0.2f, 1f);
                }
                bool clicked = GUI.Button(iconRect, new GUIContent(string.Empty, itemId));
                GUI.backgroundColor = originalBackground;
                Sprite icon = LoadoutIconResolver.Resolve(itemId);
                if (icon != null)
                {
                    LoadoutIconResolver.Draw(icon, new Rect(iconRect.x + 4f, iconRect.y + 4f, iconRect.width - 8f, iconRect.height - 8f));
                }
                if (clicked)
                {
                    _editorIconItemId = itemId;
                }
            }
            GUI.EndScrollView();

            if (!string.IsNullOrEmpty(_editorError))
            {
                GUI.Label(new Rect(12f, 268f, _editorRect.width - 24f, 20f), _editorError);
            }

            if (GUI.Button(new Rect(_editorRect.width - 176f, 294f, 78f, 27f), "Cancel"))
            {
                _editorOpen = false;
                IsModalEditorOpen = false;
            }
            if (GUI.Button(new Rect(_editorRect.width - 92f, 294f, 80f, 27f), "Save"))
            {
                SaveEditor();
            }
            GUI.DragWindow(new Rect(0f, 0f, _editorRect.width, 22f));
        }

        private void SaveEditor()
        {
            try
            {
                if (_editorCreatesPreset)
                {
                    _pendingNewPreset.IconItemId = _editorIconItemId;
                    PresetStore.Save(_pendingNewPreset, _editorName);
                    _selectedPresetName = _pendingNewPreset.Name;
                }
                else
                {
                    PresetStore.UpdateMetadata(_editorOriginalName, _editorName, _editorIconItemId);
                    _selectedPresetName = _editorName.Trim();
                }

                _editorOpen = false;
                IsModalEditorOpen = false;
                RefreshPresetCache();
                Notify("Saved loadout: " + _selectedPresetName);
            }
            catch (Exception exception)
            {
                _editorError = exception.Message;
            }
        }

        private void ReplacePresetWithCurrent(string presetName)
        {
            RunSafely(() =>
            {
                LoadoutPreset previous = FindPreset(presetName);
                LoadoutPreset replacement = LoadoutSnapshot.Capture(_mercenary);
                List<string> candidates = GetIconCandidates(replacement);
                if (previous != null && candidates.Contains(previous.IconItemId, StringComparer.Ordinal))
                {
                    replacement.IconItemId = previous.IconItemId;
                }
                PresetStore.Save(replacement, string.IsNullOrWhiteSpace(presetName) ? PresetStore.DefaultPresetName : presetName);
                _selectedPresetName = replacement.Name;
                RefreshPresetCache();
                Notify("Replaced loadout with current inventory: " + replacement.Name);
            });
        }

        private void SelectAndApplyPreset(string presetName)
        {
            _selectedPresetName = presetName;
            PresetStore.SetActivePreset(presetName);
            ApplyPreset(presetName);
        }

        private void ApplyPreset(string presetName)
        {
            RunSafely(() =>
            {
                LoadoutPreset preset = FindPreset(presetName);
                if (preset == null)
                {
                    ShowAlert($"No '{presetName}' loadout exists yet. Use the + slot to save the current inventory first.");
                    return;
                }

                GetSpaceState(out MagnumCargo cargo, out SpaceTime spaceTime);
                OperationResult result = LoadoutApplier.Apply(preset, _mercenary, cargo, spaceTime);
                _screen.RefreshView();
                ReportResult($"Applied '{preset.Name}'", result);
            });
        }

        private void NormalizePreset()
        {
            RunSafely(() =>
            {
                LoadoutPreset preset = FindPreset(_selectedPresetName);
                if (preset == null)
                {
                    ShowAlert("Select or create a loadout before normalizing.");
                    return;
                }

                GetSpaceState(out MagnumCargo cargo, out SpaceTime spaceTime);
                OperationResult result = LoadoutApplier.Normalize(preset, _mercenary, cargo, spaceTime);
                _screen.RefreshView();
                ReportResult($"Normalized '{preset.Name}'", result);
            });
        }

        private void CyclePreset(int direction)
        {
            RunSafely(() =>
            {
                if (_presets.Count == 0)
                {
                    Notify("No loadouts saved yet.");
                    return;
                }

                int index = _presets.FindIndex(preset =>
                    string.Equals(preset.Name, _selectedPresetName, StringComparison.OrdinalIgnoreCase));
                index = index < 0 ? 0 : (index + direction + _presets.Count) % _presets.Count;
                _selectedPresetName = _presets[index].Name;
                PresetStore.SetActivePreset(_selectedPresetName);
                Notify("Selected loadout: " + _selectedPresetName);
            });
        }

        private void RefreshPresetCache()
        {
            _presets = PresetStore.GetPresets();
            string activeName = PresetStore.GetActivePresetName();
            if (_presets.Any(preset => string.Equals(preset.Name, _selectedPresetName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
            _selectedPresetName = activeName;
        }

        private LoadoutPreset FindPreset(string name)
        {
            return _presets.FirstOrDefault(preset =>
                string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private string SuggestNewPresetName()
        {
            const string baseName = "New Loadout";
            if (FindPreset(baseName) == null)
            {
                return baseName;
            }
            for (int suffix = 2; suffix < 1000; suffix++)
            {
                string candidate = baseName + " " + suffix;
                if (FindPreset(candidate) == null)
                {
                    return candidate;
                }
            }
            return "Loadout " + DateTime.Now.ToString("HHmmss");
        }

        private static List<string> GetIconCandidates(LoadoutPreset preset)
        {
            IEnumerable<string> equipment = (preset.Equipment ?? new List<EquipmentPreset>()).Select(item => item.ItemId);
            IEnumerable<string> backpack = (preset.Backpack ?? new List<ItemQuantityPreset>()).Select(item => item.ItemId);
            IEnumerable<string> vest = (preset.Vest ?? new List<ItemQuantityPreset>()).Select(item => item.ItemId);
            return equipment.Concat(backpack).Concat(vest)
                .Where(itemId => !string.IsNullOrEmpty(itemId))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static string GetEffectiveIconItemId(LoadoutPreset preset)
        {
            List<string> candidates = GetIconCandidates(preset);
            if (!string.IsNullOrEmpty(preset.IconItemId) && candidates.Contains(preset.IconItemId, StringComparer.Ordinal))
            {
                return preset.IconItemId;
            }

            return (preset.Equipment ?? new List<EquipmentPreset>())
                .FirstOrDefault(item => item.Slot == "PrimaryWeapon" && !string.IsNullOrEmpty(item.ItemId))?.ItemId
                ?? (preset.Equipment ?? new List<EquipmentPreset>())
                    .FirstOrDefault(item => item.Slot == "SecondaryWeapon" && !string.IsNullOrEmpty(item.ItemId))?.ItemId
                ?? candidates.FirstOrDefault();
        }

        private static GUIStyle GetCenteredLabelStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
            return style;
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
                HandleFailure(exception);
            }
            finally
            {
                _busy = false;
            }
        }

        private static void HandleFailure(Exception exception)
        {
            Debug.LogError("[QuasimorphLoadouts] Loadout operation failed.");
            Debug.LogException(exception);
            ShowAlert("Loadout operation failed safely. No further items were moved.\n\n" + exception.Message);
        }

        private static void ReportResult(string action, OperationResult result)
        {
            if (result.Problems.Count > 0)
            {
                ShowAlert(result.ToDisplayText());
                return;
            }

            Notify($"{action}: {result.EquipmentChanged} equipment change(s), "
                + $"{result.QuantityUnloaded} item(s) unloaded, {result.QuantityMoved} item(s) added.");
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

        private void OnDisable()
        {
            _editorOpen = false;
            IsModalEditorOpen = false;
        }
    }
}
