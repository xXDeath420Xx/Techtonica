using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace TechtonicaDedicatedServer.Networking
{
    /// <summary>
    /// Manages auto-loading of save files for dedicated server mode.
    /// This allows the server to automatically load a world and start hosting.
    /// </summary>
    public static class AutoLoadManager
    {
        private static bool _hasAttemptedAutoLoad;
        private static bool _isAutoLoading;

        public static bool IsAutoLoading => _isAutoLoading;

        /// <summary>
        /// Attempts to auto-load a save file if configured.
        /// Called from the main menu patch.
        /// </summary>
        public static void TryAutoLoad()
        {
            Plugin.DebugLog("[AutoLoad] TryAutoLoad called!");

            if (_hasAttemptedAutoLoad)
            {
                Plugin.DebugLog("[AutoLoad] Already attempted, skipping");
                return;
            }
            _hasAttemptedAutoLoad = true;

            // Check if auto-load is configured
            bool hasAutoLoadPath = !string.IsNullOrEmpty(Plugin.AutoLoadSave.Value);
            bool hasAutoLoadSlot = Plugin.AutoLoadSlot.Value >= 0;

            Plugin.DebugLog($"[AutoLoad] hasAutoLoadPath={hasAutoLoadPath} ({Plugin.AutoLoadSave.Value}), hasAutoLoadSlot={hasAutoLoadSlot} ({Plugin.AutoLoadSlot.Value})");

            if (!hasAutoLoadPath && !hasAutoLoadSlot)
            {
                Plugin.DebugLog("[AutoLoad] No auto-load configured");
                return;
            }

            if (!Plugin.AutoStartServer.Value)
            {
                Plugin.DebugLog("[AutoLoad] AutoLoadSave/Slot specified but AutoStartServer is false. Skipping auto-load.");
                return;
            }

            Plugin.DebugLog("[AutoLoad] Starting auto-load process...");
            _isAutoLoading = true;

            // Try direct load first (for main thread context), then coroutine
            Plugin.DebugLog("[AutoLoad] Attempting direct (non-coroutine) auto-load...");
            TryAutoLoadDirect();
        }

        /// <summary>
        /// Direct (non-coroutine) version of auto-load for when called from non-main thread.
        /// </summary>
        private static void TryAutoLoadDirect()
        {
            Plugin.DebugLog("[AutoLoad-Direct] Starting direct auto-load...");

            // Find FlowManager type
            var flowManagerType = AccessTools.TypeByName("FlowManager");
            if (flowManagerType == null)
            {
                Plugin.DebugLog("[AutoLoad-Direct] ERROR: FlowManager type not found!");
                return;
            }

            Plugin.DebugLog($"[AutoLoad-Direct] Found FlowManager type: {flowManagerType.FullName}");

            // Check if FlowManager.instance exists
            var instanceField = flowManagerType.GetField("instance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            var flowManager = instanceField?.GetValue(null);

            Plugin.DebugLog($"[AutoLoad-Direct] FlowManager.instance: {(flowManager != null ? "exists" : "NULL")}");

            if (flowManager == null)
            {
                Plugin.DebugLog("[AutoLoad-Direct] FlowManager not ready yet - we may need to wait");
                // FlowManager isn't available yet, we can't load directly
                return;
            }

            // Load save file
            object saveState = null;
            if (!string.IsNullOrEmpty(Plugin.AutoLoadSave.Value))
            {
                saveState = LoadSaveFromPath(Plugin.AutoLoadSave.Value);
            }
            else if (Plugin.AutoLoadSlot.Value >= 0)
            {
                saveState = LoadSaveFromSlot(Plugin.AutoLoadSlot.Value);
            }

            if (saveState == null)
            {
                Plugin.DebugLog("[AutoLoad-Direct] ERROR: Failed to load save file!");
                return;
            }

            Plugin.DebugLog($"[AutoLoad-Direct] Save loaded successfully! Type: {saveState.GetType().Name}");

            // Call FlowManager.LoadSaveGame(saveState)
            var loadSaveGameMethod = flowManagerType.GetMethod("LoadSaveGame",
                BindingFlags.Public | BindingFlags.Static);

            if (loadSaveGameMethod == null)
            {
                Plugin.DebugLog("[AutoLoad-Direct] ERROR: FlowManager.LoadSaveGame method not found!");
                return;
            }

            Plugin.DebugLog("[AutoLoad-Direct] Calling FlowManager.LoadSaveGame...");

            try
            {
                // LoadSaveGame(SaveState loadingState, Action callback = null, bool asClient = false)
                loadSaveGameMethod.Invoke(null, new object[] { saveState, null, false });
                Plugin.DebugLog("[AutoLoad-Direct] LoadSaveGame called successfully!");

                // Now we need to start the server after a delay
                // Since we can't use coroutines, we'll use another thread
                var serverThread = new Thread(() =>
                {
                    Plugin.DebugLog("[AutoLoad-Direct] Server thread waiting 10 seconds for world to load...");
                    Thread.Sleep(10000); // Wait 10 seconds for world to load
                    Plugin.DebugLog("[AutoLoad-Direct] Starting server...");

                    try
                    {
                        if (DirectConnectManager.StartServer())
                        {
                            var addr = DirectConnectManager.GetServerAddress();
                            Plugin.DebugLog($"[AutoLoad-Direct] Server started successfully on {addr}");
                        }
                        else
                        {
                            Plugin.DebugLog("[AutoLoad-Direct] ERROR: Failed to start server!");
                        }
                    }
                    catch (Exception serverEx)
                    {
                        Plugin.DebugLog($"[AutoLoad-Direct] Server start error: {serverEx.Message}");
                    }

                    _isAutoLoading = false;
                });
                serverThread.IsBackground = true;
                serverThread.Start();
            }
            catch (Exception ex)
            {
                Plugin.DebugLog($"[AutoLoad-Direct] LoadSaveGame error: {ex}");
            }
        }

        private static IEnumerator AutoLoadCoroutine()
        {
            Plugin.DebugLog("[AutoLoad] AutoLoadCoroutine started!");

            // Wait for the game to fully initialize (use realtime for menu)
            yield return new WaitForSecondsRealtime(2f);

            Plugin.DebugLog("[AutoLoad] Looking for FlowManager type...");

            // Wait for FlowManager to be ready
            var flowManagerType = AccessTools.TypeByName("FlowManager");
            if (flowManagerType == null)
            {
                Plugin.DebugLog("[AutoLoad] ERROR: FlowManager type not found!");
                _isAutoLoading = false;
                yield break;
            }

            Plugin.DebugLog($"[AutoLoad] Found FlowManager type: {flowManagerType.FullName}");

            var instanceField = flowManagerType.GetField("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            if (instanceField == null)
            {
                Plugin.DebugLog("[AutoLoad] ERROR: FlowManager.instance field not found!");
                _isAutoLoading = false;
                yield break;
            }

            Plugin.DebugLog("[AutoLoad] Waiting for FlowManager.instance...");

            object flowManager = null;
            int waitAttempts = 0;
            while (flowManager == null && waitAttempts < 30)
            {
                flowManager = instanceField.GetValue(null);
                if (flowManager == null)
                {
                    if (waitAttempts % 5 == 0)
                    {
                        Plugin.DebugLog($"[AutoLoad] Still waiting for FlowManager.instance... attempt {waitAttempts}");
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                    waitAttempts++;
                }
            }

            if (flowManager == null)
            {
                Plugin.DebugLog("[AutoLoad] ERROR: FlowManager.instance is null after waiting!");
                _isAutoLoading = false;
                yield break;
            }

            Plugin.DebugLog("[AutoLoad] FlowManager ready, loading save...");

            // Try to load save
            object saveState = null;

            Plugin.DebugLog($"[AutoLoad] Attempting to load save. Path='{Plugin.AutoLoadSave.Value}', Slot={Plugin.AutoLoadSlot.Value}");

            if (!string.IsNullOrEmpty(Plugin.AutoLoadSave.Value))
            {
                Plugin.DebugLog($"[AutoLoad] Loading from path: {Plugin.AutoLoadSave.Value}");
                saveState = LoadSaveFromPath(Plugin.AutoLoadSave.Value);
            }
            else if (Plugin.AutoLoadSlot.Value >= 0)
            {
                Plugin.DebugLog($"[AutoLoad] Loading from slot: {Plugin.AutoLoadSlot.Value}");
                saveState = LoadSaveFromSlot(Plugin.AutoLoadSlot.Value);
            }

            if (saveState == null)
            {
                Plugin.DebugLog("[AutoLoad] ERROR: Failed to load save file!");
                _isAutoLoading = false;
                yield break;
            }

            Plugin.DebugLog($"[AutoLoad] Save loaded successfully! Type: {saveState.GetType().Name}");

            // Call FlowManager.LoadSaveGame(saveState)
            var loadSaveGameMethod = flowManagerType.GetMethod("LoadSaveGame",
                BindingFlags.Public | BindingFlags.Static);

            if (loadSaveGameMethod == null)
            {
                Plugin.Log.LogError("[AutoLoad] FlowManager.LoadSaveGame method not found!");
                _isAutoLoading = false;
                yield break;
            }

            // Create callback to start server after load
            Action onLoadComplete = () =>
            {
                Plugin.Log.LogInfo("[AutoLoad] Game loaded, starting server...");
                Plugin.Instance.StartCoroutine(StartServerAfterDelay());
            };

            try
            {
                // LoadSaveGame(SaveState loadingState, Action callback = null, bool asClient = false)
                loadSaveGameMethod.Invoke(null, new object[] { saveState, onLoadComplete, false });
                Plugin.Log.LogInfo("[AutoLoad] LoadSaveGame called successfully");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AutoLoad] Failed to call LoadSaveGame: {ex}");
                _isAutoLoading = false;
            }
        }

        private static IEnumerator StartServerAfterDelay()
        {
            // Wait for the world to fully load
            yield return new WaitForSecondsRealtime(5f);

            Plugin.Log.LogInfo("[AutoLoad] Starting dedicated server...");

            if (DirectConnectManager.StartServer())
            {
                var addr = DirectConnectManager.GetServerAddress();
                Plugin.Log.LogInfo($"[AutoLoad] Server started successfully on {addr}");
            }
            else
            {
                Plugin.Log.LogError("[AutoLoad] Failed to start server!");
            }

            _isAutoLoading = false;
        }

        private static object LoadSaveFromPath(string path)
        {
            try
            {
                Plugin.DebugLog($"[AutoLoad] Loading save from path: {path}");

                // Check if file exists
                if (!File.Exists(path))
                {
                    Plugin.DebugLog($"[AutoLoad] ERROR: Save file does not exist at path: {path}");
                    return null;
                }

                Plugin.DebugLog($"[AutoLoad] Save file exists, size: {new FileInfo(path).Length} bytes");

                var saveStateType = AccessTools.TypeByName("SaveState");
                if (saveStateType == null)
                {
                    Plugin.DebugLog("[AutoLoad] ERROR: SaveState type not found!");
                    return null;
                }

                Plugin.DebugLog($"[AutoLoad] Found SaveState type: {saveStateType.FullName}");

                // List all available methods for debugging
                var methods = saveStateType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                Plugin.DebugLog($"[AutoLoad] SaveState has {methods.Length} methods. Looking for load methods...");

                foreach (var m in methods)
                {
                    if (m.Name.Contains("Load"))
                    {
                        var parms = string.Join(", ", Array.ConvertAll(m.GetParameters(), p => $"{p.ParameterType.Name} {p.Name}"));
                        Plugin.DebugLog($"[AutoLoad]   Found: {m.Name}({parms})");
                    }
                }

                // Try LoadFileDataFromAbsolutePath FIRST since we're using absolute paths
                var loadAbsMethod = saveStateType.GetMethod("LoadFileDataFromAbsolutePath",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

                if (loadAbsMethod != null)
                {
                    Plugin.DebugLog("[AutoLoad] Using LoadFileDataFromAbsolutePath method");
                    var result = loadAbsMethod.Invoke(null, new object[] { path });
                    Plugin.DebugLog($"[AutoLoad] LoadFileDataFromAbsolutePath returned: {result?.GetType().Name ?? "null"}");
                    if (result != null) return result;
                    Plugin.DebugLog("[AutoLoad] LoadFileDataFromAbsolutePath returned null, trying other methods...");
                }

                // Try with Wine Z: drive path (absolute paths become Z:\path\to\file)
                string winePath = "Z:" + path.Replace("/", "\\");
                Plugin.DebugLog($"[AutoLoad] Trying with Wine path: {winePath}");

                // Try LoadFileData(string saveLocation) with Wine path
                var loadMethod = saveStateType.GetMethod("LoadFileData",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(string) },
                    null);

                if (loadMethod != null)
                {
                    Plugin.DebugLog("[AutoLoad] Using LoadFileData(string) with Wine path");
                    var result = loadMethod.Invoke(null, new object[] { winePath });
                    Plugin.DebugLog($"[AutoLoad] LoadFileData(winePath) returned: {result?.GetType().Name ?? "null"}");
                    if (result != null) return result;

                    // Also try original Unix path
                    Plugin.DebugLog("[AutoLoad] Trying LoadFileData with original Unix path");
                    result = loadMethod.Invoke(null, new object[] { path });
                    Plugin.DebugLog($"[AutoLoad] LoadFileData(unixPath) returned: {result?.GetType().Name ?? "null"}");
                    if (result != null) return result;
                }

                Plugin.DebugLog("[AutoLoad] ERROR: No suitable load method found!");
                return null;
            }
            catch (Exception ex)
            {
                Plugin.DebugLog($"[AutoLoad] ERROR loading save from path: {ex}");
                return null;
            }
        }

        private static object LoadSaveFromSlot(int slot)
        {
            try
            {
                Plugin.Log.LogInfo($"[AutoLoad] Loading save from slot: {slot}");

                var saveStateType = AccessTools.TypeByName("SaveState");
                if (saveStateType == null)
                {
                    Plugin.Log.LogError("[AutoLoad] SaveState type not found!");
                    return null;
                }

                // Get saves in slot
                var getSavesMethod = saveStateType.GetMethod("GetSavesInSlot",
                    BindingFlags.Public | BindingFlags.Static);

                if (getSavesMethod == null)
                {
                    Plugin.Log.LogError("[AutoLoad] GetSavesInSlot method not found!");
                    return null;
                }

                var saves = getSavesMethod.Invoke(null, new object[] { slot }) as System.Collections.IList;
                if (saves == null || saves.Count == 0)
                {
                    Plugin.Log.LogError($"[AutoLoad] No saves found in slot {slot}!");
                    return null;
                }

                // Get the most recent save (first in list)
                var saveMetadata = saves[0];
                Plugin.Log.LogInfo($"[AutoLoad] Found save in slot {slot}");

                // Load the save using metadata
                var loadMethod = saveStateType.GetMethod("LoadFileData",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { saveMetadata.GetType(), typeof(string) },
                    null);

                if (loadMethod != null)
                {
                    return loadMethod.Invoke(null, new object[] { saveMetadata, null });
                }

                Plugin.Log.LogError("[AutoLoad] LoadFileData(metadata) method not found!");
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[AutoLoad] Error loading save from slot: {ex}");
                return null;
            }
        }
    }

    /// <summary>
    /// Harmony's AccessTools-like helper for finding types.
    /// </summary>
    public static class AccessTools
    {
        public static Type TypeByName(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = asm.GetType(name);
                    if (type != null) return type;

                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == name) return t;
                    }
                }
                catch
                {
                    // Ignore assemblies that can't be searched
                }
            }
            return null;
        }
    }
}
