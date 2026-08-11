using System;
using System.Reflection;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace QuasimorphLoadouts
{
    public static class Plugin
    {
        private const string HarmonyId = "radicalgitter.quasimorph-loadouts";

        [Hook(ModHookType.AfterConfigsLoaded)]
        public static void Initialize(IModContext context)
        {
            try
            {
                new Harmony(HarmonyId).PatchAll(Assembly.GetExecutingAssembly());
                Debug.Log($"[QuasimorphLoadouts] Loaded. Presets: {PresetStore.PresetPath}");
            }
            catch (Exception exception)
            {
                Debug.LogError("[QuasimorphLoadouts] Failed to initialize.");
                Debug.LogException(exception);
            }
        }
    }
}
