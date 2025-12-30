using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace TechtonicaDedicatedServer.Patches
{
    /// <summary>
    /// Patches to enable headless (no graphics) mode for dedicated servers.
    /// These patches null out or bypass graphics-dependent code.
    /// </summary>
    public static class HeadlessPatches
    {
        private static bool _patchesApplied;

        public static void ApplyPatches(Harmony harmony)
        {
            if (_patchesApplied) return;
            if (!Plugin.HeadlessMode.Value) return;

            try
            {
                Plugin.Log.LogInfo("[HeadlessPatches] Applying headless mode patches...");

                // Disable cursor locking (causes issues in headless)
                PatchCursorLock(harmony);

                // Disable audio if running headless
                PatchAudio(harmony);

                // Disable camera rendering
                PatchCamera(harmony);

                // Disable UI updates
                PatchUI(harmony);

                _patchesApplied = true;
                Plugin.Log.LogInfo("[HeadlessPatches] Headless patches applied");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[HeadlessPatches] Failed to apply patches: {ex}");
            }
        }

        private static void PatchCursorLock(Harmony harmony)
        {
            try
            {
                // Prevent cursor lock operations
                var lockStateProperty = typeof(Cursor).GetProperty("lockState");
                if (lockStateProperty != null)
                {
                    var setter = lockStateProperty.GetSetMethod();
                    if (setter != null)
                    {
                        var prefix = new HarmonyMethod(typeof(HeadlessPatches), nameof(CursorLock_Prefix));
                        harmony.Patch(setter, prefix: prefix);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[HeadlessPatches] Cursor patch failed: {ex.Message}");
            }
        }

        private static void PatchAudio(Harmony harmony)
        {
            try
            {
                // Disable AudioListener if present - use reflection to avoid assembly reference issues
                var audioListenerType = AccessTools.TypeByName("UnityEngine.AudioListener");
                if (audioListenerType == null) return;

                var awakeMethod = audioListenerType.GetMethod("Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (awakeMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(HeadlessPatches), nameof(DisableComponent_Prefix));
                    harmony.Patch(awakeMethod, prefix: prefix);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[HeadlessPatches] Audio patch failed: {ex.Message}");
            }
        }

        private static void PatchCamera(Harmony harmony)
        {
            try
            {
                // We could disable camera rendering, but this might break
                // game logic that depends on camera positions
                // For now, just log that we're in headless mode
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[HeadlessPatches] Camera patch failed: {ex.Message}");
            }
        }

        private static void PatchUI(Harmony harmony)
        {
            try
            {
                // Find and patch UI manager if it exists
                var uiManagerType = AccessTools.TypeByName("UIManager") ??
                                   AccessTools.TypeByName("UIController") ??
                                   AccessTools.TypeByName("HUDManager");

                if (uiManagerType != null)
                {
                    var updateMethod = AccessTools.Method(uiManagerType, "Update");
                    if (updateMethod != null)
                    {
                        var prefix = new HarmonyMethod(typeof(HeadlessPatches), nameof(SkipMethod_Prefix));
                        harmony.Patch(updateMethod, prefix: prefix);
                        Plugin.Log.LogInfo("[HeadlessPatches] UI updates disabled");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[HeadlessPatches] UI patch failed: {ex.Message}");
            }
        }

        // Patch methods
        public static bool CursorLock_Prefix()
        {
            // Skip cursor lock in headless mode
            return false;
        }

        public static bool DisableComponent_Prefix(MonoBehaviour __instance)
        {
            // Disable the component instead of running Awake
            if (__instance != null)
            {
                __instance.enabled = false;
            }
            return false;
        }

        public static bool SkipMethod_Prefix()
        {
            // Skip the method entirely
            return false;
        }
    }

    /// <summary>
    /// Patches for auto-starting the server when configured.
    /// Hooks into the main menu to trigger auto-load of save files.
    /// </summary>
    public static class AutoStartPatches
    {
        private static bool _hasTriggeredAutoLoad;
        private static int _updateCount;
        private static float _startTime;

        public static void ApplyPatches(Harmony harmony)
        {
            try
            {
                _startTime = Time.realtimeSinceStartup;

                // Hook into MainMenuManager or similar to trigger auto-load
                var mainMenuType = AccessTools.TypeByName("MainMenuManager") ??
                                  AccessTools.TypeByName("MainMenu") ??
                                  AccessTools.TypeByName("TitleScreenManager");

                if (mainMenuType != null)
                {
                    var startMethod = AccessTools.Method(mainMenuType, "Start") ??
                                     AccessTools.Method(mainMenuType, "Awake");

                    if (startMethod != null)
                    {
                        var postfix = new HarmonyMethod(typeof(AutoStartPatches), nameof(MainMenu_Postfix));
                        harmony.Patch(startMethod, postfix: postfix);
                        Plugin.Log.LogInfo("[AutoStartPatches] Hooked main menu for auto-load");
                    }

                    // IMPORTANT: Also hook the Update method for continuous checking
                    var updateMethod = AccessTools.Method(mainMenuType, "Update");
                    if (updateMethod != null)
                    {
                        var updatePostfix = new HarmonyMethod(typeof(AutoStartPatches), nameof(MainMenu_Update_Postfix));
                        harmony.Patch(updateMethod, postfix: updatePostfix);
                        Plugin.Log.LogInfo("[AutoStartPatches] Hooked MainMenu.Update for auto-load polling");
                    }
                }

                // Also hook FlowManager.Start as a fallback
                var flowManagerType = AccessTools.TypeByName("FlowManager");
                if (flowManagerType != null)
                {
                    var startMethod = AccessTools.Method(flowManagerType, "Start");
                    if (startMethod != null)
                    {
                        var postfix = new HarmonyMethod(typeof(AutoStartPatches), nameof(FlowManager_Start_Postfix));
                        harmony.Patch(startMethod, postfix: postfix);
                        Plugin.Log.LogInfo("[AutoStartPatches] Hooked FlowManager.Start for auto-load");
                    }

                    // Also hook FlowManager.Update for polling
                    var updateMethod = AccessTools.Method(flowManagerType, "Update");
                    if (updateMethod != null)
                    {
                        var updatePostfix = new HarmonyMethod(typeof(AutoStartPatches), nameof(FlowManager_Update_Postfix));
                        harmony.Patch(updateMethod, postfix: updatePostfix);
                        Plugin.Log.LogInfo("[AutoStartPatches] Hooked FlowManager.Update for auto-load polling");
                    }
                    else
                    {
                        Plugin.Log.LogWarning("[AutoStartPatches] FlowManager.Update method not found!");
                    }
                }

                // Try hooking into EventSystem.Update (UI input processing)
                try
                {
                    var eventSystemType = AccessTools.TypeByName("UnityEngine.EventSystems.EventSystem");
                    if (eventSystemType != null)
                    {
                        var esUpdateMethod = AccessTools.Method(eventSystemType, "Update");
                        if (esUpdateMethod != null)
                        {
                            var esPostfix = new HarmonyMethod(typeof(AutoStartPatches), nameof(EventSystem_Update_Postfix));
                            harmony.Patch(esUpdateMethod, postfix: esPostfix);
                            Plugin.Log.LogInfo("[AutoStartPatches] Hooked EventSystem.Update for auto-load polling");
                        }
                        else
                        {
                            Plugin.Log.LogWarning("[AutoStartPatches] EventSystem.Update not found");
                        }
                    }
                    else
                    {
                        Plugin.Log.LogWarning("[AutoStartPatches] EventSystem type not found");
                    }
                }
                catch (Exception hookEx)
                {
                    Plugin.Log.LogWarning($"[AutoStartPatches] EventSystem hook failed: {hookEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AutoStartPatches] Failed to apply patches: {ex}");
            }
        }

        public static void MainMenu_Postfix()
        {
            TriggerAutoLoad("MainMenu.Start");
        }

        public static void FlowManager_Start_Postfix()
        {
            Plugin.DebugLog("[AutoStartPatches] FlowManager.Start postfix called!");
            TriggerAutoLoad("FlowManager.Start");
        }

        public static void MainMenu_Update_Postfix()
        {
            CheckAutoLoadTrigger("MainMenu.Update");
        }

        private static bool _firstFlowManagerUpdateCall = true;

        public static void FlowManager_Update_Postfix()
        {
            if (_firstFlowManagerUpdateCall)
            {
                _firstFlowManagerUpdateCall = false;
                Plugin.DebugLog("[AutoStartPatches] FlowManager.Update postfix FIRST CALL!");
            }
            CheckAutoLoadTrigger("FlowManager.Update");
        }

        private static bool _firstEventSystemCall = true;

        public static void EventSystem_Update_Postfix()
        {
            if (_firstEventSystemCall)
            {
                _firstEventSystemCall = false;
                Plugin.DebugLog("[AutoStartPatches] EventSystem.Update postfix FIRST CALL!");
            }
            CheckAutoLoadTrigger("EventSystem.Update");
        }

        private static void CheckAutoLoadTrigger(string source)
        {
            _updateCount++;

            // Log periodically to confirm hooks are working
            if (_updateCount % 300 == 0)
            {
                var elapsed = Time.realtimeSinceStartup - _startTime;
                Plugin.DebugLog($"[AutoStartPatches] {source} called {_updateCount} times, elapsed: {elapsed:F1}s");
            }

            // Check if thread triggered auto-load (15 seconds after start)
            var timeSinceStart = Time.realtimeSinceStartup - _startTime;
            if (!_hasTriggeredAutoLoad && timeSinceStart > 15f)
            {
                TriggerAutoLoad($"{source} (time-based)");
            }
        }

        private static void TriggerAutoLoad(string source)
        {
            if (_hasTriggeredAutoLoad) return;
            _hasTriggeredAutoLoad = true;

            // Check if auto-load is configured
            if (Plugin.AutoStartServer.Value &&
                (!string.IsNullOrEmpty(Plugin.AutoLoadSave.Value) || Plugin.AutoLoadSlot.Value >= 0))
            {
                Plugin.DebugLog($"[AutoStartPatches] Triggering auto-load from {source}!");
                Networking.AutoLoadManager.TryAutoLoad();
            }
        }
    }
}
