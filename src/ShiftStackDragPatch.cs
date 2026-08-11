using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MGSC;
using TMPro;
using UnityEngine;

namespace QuasimorphLoadouts
{
    [HarmonyPatch]
    internal static class ShiftStackDragPatch
    {
        private static readonly FieldInfo DraggableItemField = AccessTools.Field(typeof(DragController), "_draggableItem");
        private static readonly FieldInfo DragModeField = AccessTools.Field(typeof(DragController), "_dragMode");
        private static readonly FieldInfo LastStorageField = AccessTools.Field(typeof(DragController), "_lastStorage");
        private static readonly FieldInfo SpecPositionField = AccessTools.Field(typeof(DragController), "_specPosition");
        private static readonly FieldInfo SpaceTimeField = AccessTools.Field(typeof(DragController), "_spaceTime");
        private static readonly FieldInfo RefreshCallbackField = AccessTools.Field(typeof(DragController), "_refreshCallback");
        private static readonly FieldInfo MouseDownSlotField = AccessTools.Field(typeof(DragController), "_slotUnderCursorWhenMouseDown");
        private static readonly MethodInfo RaycastSlotMethod = AccessTools.Method(typeof(DragController), "RaycastSlotUnderCursor");
        private static readonly MethodInfo RefreshVisualMethod = AccessTools.Method(typeof(DragController), "RefreshVisual");
        private static readonly MethodInfo RefreshPositionMethod = AccessTools.Method(typeof(DragController), "RefreshPosition");

        private static DragController _customController;
        private static bool _suppressNextLeftMouseUp;

        internal static bool IsCustomDragging(DragController controller)
        {
            return controller != null && controller == _customController && controller.IsDragging;
        }

        [HarmonyPatch(typeof(DragController), "Update")]
        [HarmonyPrefix]
        private static bool DragControllerUpdatePrefix(DragController __instance)
        {
            if (__instance == null || LoadoutHotkeyController.IsModalEditorOpen || UI.IsShowing<CommonContextMenu>())
            {
                return true;
            }

            if (__instance == _customController && _suppressNextLeftMouseUp && Input.GetMouseButtonUp(0))
            {
                _suppressNextLeftMouseUp = false;
                return false;
            }

            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!shiftHeld || !Input.GetMouseButtonDown(0))
            {
                return true;
            }

            ItemSlot slot = RaycastSlotMethod.Invoke(__instance, new object[] { true }) as ItemSlot;
            BasePickupItem source = slot?.Item;
            if (slot == null || source == null || !slot.IsDraggable || source.Locked)
            {
                return true;
            }

            if (!__instance.IsDragging)
            {
                if (!source.IsStackable || source.StackCount <= 1)
                {
                    return true;
                }

                if (BeginOneItemDrag(__instance, slot, source))
                {
                    _suppressNextLeftMouseUp = true;
                    return false;
                }
                return true;
            }

            if (__instance != _customController)
            {
                return true;
            }

            AddOneToDraggedStack(__instance, source);
            _suppressNextLeftMouseUp = true;
            return false;
        }

        private static bool BeginOneItemDrag(DragController controller, ItemSlot slot, BasePickupItem source)
        {
            ItemStorage originalStorage = source.Storage;
            if (originalStorage == null)
            {
                return false;
            }

            BasePickupItem lifted = null;
            bool sourceDecremented = false;
            try
            {
                lifted = CreateOneFrom(source);
                if (lifted == null)
                {
                    return false;
                }

                ItemContentDescriptor descriptor = lifted.View<ItemContentDescriptor>();
                Sprite icon = SingletonMonoBehaviour<ItemFactory>.Instance.ResolveIcon(descriptor, 1);
                source.StackCount -= 1;
                sourceDecremented = true;
                ItemInteractionSystem.TrySplitUsable(source, lifted);
                DraggableItemField.SetValue(controller, lifted);
                DragModeField.SetValue(controller, DragMode.Dragging);
                LastStorageField.SetValue(controller, originalStorage);
                SpecPositionField.SetValue(controller, source.InventoryPos);
                MouseDownSlotField.SetValue(controller, slot);
                _customController = controller;
                InvokeRefresh(controller);
                RefreshVisual(controller, icon);
                RefreshPositionMethod.Invoke(controller, null);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[QuasimorphLoadouts] Could not begin a one-item drag; restoring the split item.");
                Debug.LogException(exception);
                SpaceTime spaceTime = SpaceTimeField.GetValue(controller) as SpaceTime;
                if (sourceDecremented && lifted != null && lifted.StackCount > 0)
                {
                    try
                    {
                        RestoreFailedUnit(lifted, source, originalStorage, source.InventoryPos, false, spaceTime);
                    }
                    catch (Exception restoreException)
                    {
                        Debug.LogException(restoreException);
                    }
                }
                _customController = null;
                _suppressNextLeftMouseUp = false;
                DragModeField.SetValue(controller, DragMode.None);
                DraggableItemField.SetValue(controller, null);
                InvokeRefresh(controller);
                return false;
            }
        }

        private static void AddOneToDraggedStack(DragController controller, BasePickupItem source)
        {
            BasePickupItem dragged = controller.DraggableItem;
            if (dragged == null
                || source == dragged
                || !source.IsStackable
                || source.StackCount <= 0
                || dragged.StackCount >= dragged.MaxStack
                || !ItemInteractionSystem.CanMerge(source, dragged))
            {
                return;
            }

            ItemStorage sourceStorage = source.Storage;
            if (sourceStorage == null)
            {
                return;
            }
            CellPosition sourcePosition = source.InventoryPos;
            BasePickupItem unit = null;
            bool removedWholeSource = source.StackCount == 1;

            SpaceTime spaceTime = SpaceTimeField.GetValue(controller) as SpaceTime;
            try
            {
                if (removedWholeSource)
                {
                    unit = source;
                    sourceStorage.Remove(source, true);
                }
                else
                {
                    unit = CreateOneFrom(source);
                    if (unit == null)
                    {
                        return;
                    }
                    source.StackCount -= 1;
                    ItemInteractionSystem.TrySplitUsable(source, unit);
                }

                bool unitConsumed = false;
                if (!ItemInteractionSystem.Merge(spaceTime, unit, dragged, ref unitConsumed))
                {
                    RestoreFailedUnit(unit, source, sourceStorage, sourcePosition, removedWholeSource, spaceTime);
                    return;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[QuasimorphLoadouts] Could not add one item to the dragged stack.");
                Debug.LogException(exception);
                if (unit != null && unit.StackCount > 0)
                {
                    RestoreFailedUnit(unit, source, sourceStorage, sourcePosition, removedWholeSource, spaceTime);
                }
                return;
            }

            InvokeRefresh(controller);
            ItemContentDescriptor descriptor = dragged.View<ItemContentDescriptor>();
            Sprite icon = SingletonMonoBehaviour<ItemFactory>.Instance.ResolveIcon(descriptor, 1);
            RefreshVisual(controller, icon);
        }

        private static BasePickupItem CreateOneFrom(BasePickupItem source)
        {
            BasePickupItem unit = SingletonMonoBehaviour<ItemFactory>.Instance.CreateForInventory(source.Id);
            if (unit == null)
            {
                return null;
            }

            unit.StackCount = 1;
            unit.ExaminedItem = source.ExaminedItem;
            unit.CopyExpireTime(source);
            return unit;
        }

        private static void RestoreFailedUnit(
            BasePickupItem unit,
            BasePickupItem source,
            ItemStorage sourceStorage,
            CellPosition sourcePosition,
            bool removedWholeSource,
            SpaceTime spaceTime)
        {
            if (!removedWholeSource)
            {
                bool consumed = false;
                if (ItemInteractionSystem.Merge(spaceTime, unit, source, ref consumed))
                {
                    return;
                }
            }

            if (sourceStorage != null)
            {
                if (sourceStorage.TryPutItem(unit, sourcePosition, hasSpecialPos: true))
                {
                    return;
                }

                bool emptyAfterMerge;
                ItemInteractionSystem.TryMergeIntoStorage(sourceStorage, unit, spaceTime, out emptyAfterMerge);
                if (emptyAfterMerge || unit.StackCount <= 0 || sourceStorage.TryPutItem(unit, CellPosition.Zero))
                {
                    return;
                }
            }

            Debug.LogError("[QuasimorphLoadouts] A failed one-item merge could not be restored to its source storage.");
        }

        [HarmonyPatch(typeof(DragController), "ReturnToOriginalStorage")]
        [HarmonyPrefix]
        private static bool ReturnToOriginalStoragePrefix(DragController __instance)
        {
            if (!IsCustomDragging(__instance))
            {
                return true;
            }

            BasePickupItem dragged = __instance.DraggableItem;
            ItemStorage originalStorage = LastStorageField.GetValue(__instance) as ItemStorage;
            SpaceTime spaceTime = SpaceTimeField.GetValue(__instance) as SpaceTime;
            if (dragged == null || originalStorage == null)
            {
                return true;
            }

            bool emptyAfterMerge;
            ItemInteractionSystem.TryMergeIntoStorage(originalStorage, dragged, spaceTime, out emptyAfterMerge);
            if (emptyAfterMerge || dragged.StackCount <= 0)
            {
                return false;
            }

            CellPosition originalPosition = (CellPosition)SpecPositionField.GetValue(__instance);
            if (originalStorage.TryPutItem(dragged, originalPosition, hasSpecialPos: true)
                || originalStorage.TryPutItem(dragged, CellPosition.Zero))
            {
                return false;
            }

            Debug.LogWarning("[QuasimorphLoadouts] Could not use the safer custom return path; falling back to the game's drag return.");
            return true;
        }

        [HarmonyPatch(typeof(DragController), "ResetDragState")]
        [HarmonyPostfix]
        private static void ResetDragStatePostfix(DragController __instance)
        {
            if (__instance != _customController)
            {
                return;
            }

            _customController = null;
            _suppressNextLeftMouseUp = false;
        }

        [HarmonyPatch(typeof(DragController), nameof(DragController.Disable))]
        [HarmonyPostfix]
        private static void DisablePostfix(DragController __instance)
        {
            ResetDragStatePostfix(__instance);
        }

        [HarmonyPatch(typeof(DragController), nameof(DragController.Enable))]
        [HarmonyPostfix]
        private static void EnablePostfix(DragController __instance)
        {
            ShiftStackDragIndicator indicator = __instance.GetComponent<ShiftStackDragIndicator>();
            if (indicator == null)
            {
                indicator = __instance.gameObject.AddComponent<ShiftStackDragIndicator>();
            }
            indicator.Configure(__instance);
        }

        private static void InvokeRefresh(DragController controller)
        {
            (RefreshCallbackField.GetValue(controller) as Action)?.Invoke();
        }

        private static void RefreshVisual(DragController controller, Sprite icon)
        {
            RefreshVisualMethod.Invoke(controller, new object[] { icon, true });
        }
    }

    internal sealed class ShiftStackDragIndicator : MonoBehaviour
    {
        private DragController _controller;
        private GUIStyle _countStyle;
        private GUIStyle _shadowStyle;

        internal void Configure(DragController controller)
        {
            _controller = controller;
        }

        private void OnGUI()
        {
            if (!ShiftStackDragPatch.IsCustomDragging(_controller) || _controller.DraggableItem == null)
            {
                return;
            }

            GUI.depth = -2000;
            Vector3 mouse = Input.mousePosition;
            Rect countRect = new Rect(mouse.x - 28f, Screen.height - mouse.y + 48f, 56f, 28f);
            EnsureStyles();
            string count = _controller.DraggableItem.StackCount.ToString();
            GUI.Label(new Rect(countRect.x + 2f, countRect.y + 2f, countRect.width, countRect.height), count, _shadowStyle);
            GUI.Label(countRect, count, _countStyle);
        }

        private void EnsureStyles()
        {
            if (_countStyle != null)
            {
                return;
            }

            Font gameFont = Resources.FindObjectsOfTypeAll<TMP_Text>()
                .Select(text => text != null && text.font != null ? text.font.sourceFontFile : null)
                .FirstOrDefault(font => font != null)
                ?? GUI.skin.label.font;
            _countStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                font = gameFont,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Overflow
            };
            _countStyle.normal.textColor = new Color(0.78f, 0.96f, 0.82f, 1f);
            _shadowStyle = new GUIStyle(_countStyle);
            _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.92f);
        }
    }
}
