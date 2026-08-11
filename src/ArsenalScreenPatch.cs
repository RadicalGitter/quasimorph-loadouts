using HarmonyLib;
using MGSC;

namespace QuasimorphLoadouts
{
    [HarmonyPatch(typeof(ArsenalScreen), nameof(ArsenalScreen.Configure), typeof(Mercenary), typeof(bool))]
    internal static class ArsenalScreenPatch
    {
        private static void Postfix(ArsenalScreen __instance, Mercenary mercenary)
        {
            if (__instance == null || mercenary == null)
            {
                return;
            }

            LoadoutHotkeyController controller = __instance.GetComponent<LoadoutHotkeyController>();
            if (controller == null)
            {
                controller = __instance.gameObject.AddComponent<LoadoutHotkeyController>();
            }
            controller.Configure(__instance, mercenary);
        }
    }
}
