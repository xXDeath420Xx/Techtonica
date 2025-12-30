using HarmonyLib;
using Mirror;
using System;
using System.Reflection;
using UnityEngine;
using TechtonicaDedicatedServer.Networking;

namespace TechtonicaDedicatedServer.Patches
{
    /// <summary>
    /// Patches to prevent game from quitting when Steam is unavailable.
    /// This is essential for dedicated server mode.
    /// </summary>
    public static class SteamBypassPatches
    {
        public static bool SteamInitFailed = false;
        public static bool QuitBlocked = false;

        /// <summary>
        /// Patch SteamPlatform constructor to skip Steam initialization entirely.
        /// This allows the game to run without Steam.
        /// </summary>
        [HarmonyPatch]
        public static class SteamPlatformCtorPatch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("SteamPlatform");
                if (type == null)
                {
                    Plugin.Log.LogWarning("[SteamBypass] Could not find SteamPlatform type");
                    return null;
                }

                foreach (var ctor in type.GetConstructors())
                {
                    var parms = ctor.GetParameters();
                    if (parms.Length == 1 && parms[0].ParameterType == typeof(uint))
                    {
                        Plugin.Log.LogInfo($"[SteamBypass] Found SteamPlatform constructor");
                        return ctor;
                    }
                }
                Plugin.Log.LogWarning("[SteamBypass] Could not find SteamPlatform(uint) constructor");
                return null;
            }

            [HarmonyPrefix]
            static bool Prefix()
            {
                try
                {
                    Plugin.Log.LogInfo("[SteamBypass] SteamPlatform Prefix called!");

                    // In server mode, skip the entire SteamPlatform constructor
                    bool enableDirect = Plugin.EnableDirectConnect?.Value ?? false;
                    bool autoStart = Plugin.AutoStartServer?.Value ?? false;

                    Plugin.Log.LogInfo($"[SteamBypass] EnableDirectConnect={enableDirect}, AutoStartServer={autoStart}");

                    if (enableDirect && autoStart)
                    {
                        Plugin.Log.LogInfo("[SteamBypass] SKIPPING SteamPlatform constructor (server mode)");
                        SteamInitFailed = true;
                        return false; // Skip constructor body
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[SteamBypass] Prefix error: {ex}");
                }
                return true;
            }

            [HarmonyFinalizer]
            static Exception Finalizer(Exception __exception)
            {
                if (__exception != null)
                {
                    SteamInitFailed = true;
                    Plugin.Log.LogWarning($"[SteamBypass] Finalizer caught exception: {__exception.Message}");
                    if (Plugin.EnableDirectConnect.Value && Plugin.AutoStartServer.Value)
                    {
                        Plugin.Log.LogWarning("[SteamBypass] Suppressing exception for server mode");
                        return null;
                    }
                }
                return __exception;
            }
        }

        /// <summary>
        /// Block Application.Quit when running as dedicated server without Steam.
        /// </summary>
        [HarmonyPatch(typeof(Application), "Quit", new Type[0])]
        public static class ApplicationQuitPatch
        {
            static bool Prefix()
            {
                // Always block quit on first call when in server mode
                if (Plugin.EnableDirectConnect.Value && Plugin.AutoStartServer.Value && !QuitBlocked)
                {
                    QuitBlocked = true;
                    Plugin.Log.LogWarning("[SteamBypass] BLOCKED Application.Quit - running as dedicated server");
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Block Application.Quit(exitCode) when running as dedicated server.
        /// </summary>
        [HarmonyPatch(typeof(Application), "Quit", new Type[] { typeof(int) })]
        public static class ApplicationQuitWithCodePatch
        {
            static bool Prefix(int exitCode)
            {
                if (Plugin.EnableDirectConnect.Value && Plugin.AutoStartServer.Value && !QuitBlocked)
                {
                    QuitBlocked = true;
                    Plugin.Log.LogWarning($"[SteamBypass] BLOCKED Application.Quit({exitCode}) - running as dedicated server");
                    return false;
                }
                return true;
            }
        }
    }

    /// <summary>
    /// Harmony patches for network-related classes.
    /// Based on decompilation of TechNetworkManager, NetworkConnector, SteamLobbyConnector.
    /// </summary>
    public static class NetworkPatches
    {
        /// <summary>
        /// Patch TechNetworkManager.Awake to optionally use KCP transport instead of FizzyFacepunch.
        ///
        /// Original code (from decompilation):
        ///   FizzyFacepunch component = Object.Instantiate(customTransportPrefab, base.transform).GetComponent<FizzyFacepunch>();
        ///   transport = component;
        ///   Object.Destroy(base.gameObject.GetComponent<TelepathyTransport>());
        /// </summary>
        [HarmonyPatch]
        public static class TechNetworkManagerAwakePatch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("TechNetworkManager");
                return type?.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            static void Postfix(NetworkManager __instance)
            {
                if (!Plugin.EnableDirectConnect.Value) return;

                Plugin.Log.LogInfo("[NetworkPatches] TechNetworkManager.Awake completed");

                // Use reflection to get transport (it may be internal)
                var transportField = typeof(NetworkManager).GetField("transport",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var transport = transportField?.GetValue(__instance);
                Plugin.Log.LogInfo($"[NetworkPatches] Current transport: {transport?.GetType().Name ?? "null"}");

                // Store reference for later swapping
                // We don't swap here because we want to allow both modes
            }
        }

        /// <summary>
        /// Patch NetworkConnector.ConnectAsHost to intercept hosting.
        ///
        /// Original code (from decompilation):
        ///   isHost = true;
        ///   isClient = false;
        ///   wasHost = true;
        ///   NetworkManager.singleton.StartHost();
        ///   if (!FlowManager.isReplayMode) { StartLobby(); }
        /// </summary>
        [HarmonyPatch]
        public static class NetworkConnectorConnectAsHostPatch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("NetworkConnector");
                return type?.GetMethod("ConnectAsHost", BindingFlags.Static | BindingFlags.Public);
            }

            static bool Prefix()
            {
                // If direct connect is active and we've already started, skip original
                if (DirectConnectManager.IsDirectConnectActive && DirectConnectManager.IsServer)
                {
                    Plugin.Log.LogInfo("[NetworkPatches] Skipping ConnectAsHost - direct connect server already running");
                    return false;
                }

                return true; // Run original
            }

            static void Postfix()
            {
                if (DirectConnectManager.IsDirectConnectActive)
                {
                    Plugin.Log.LogInfo("[NetworkPatches] ConnectAsHost completed with direct connect");
                }
            }
        }

        /// <summary>
        /// Patch NetworkConnector.ConnectAsClient to intercept client connections.
        ///
        /// Original code (from decompilation):
        ///   NetworkManager.singleton.StartClient();
        ///   isHost = false;
        ///   wasHost = false;
        ///   isClient = true;
        /// </summary>
        [HarmonyPatch]
        public static class NetworkConnectorConnectAsClientPatch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("NetworkConnector");
                return type?.GetMethod("ConnectAsClient", BindingFlags.Static | BindingFlags.Public);
            }

            static bool Prefix()
            {
                // If direct connect is active and we're already connecting, skip original
                if (DirectConnectManager.IsDirectConnectActive && DirectConnectManager.IsClient)
                {
                    Plugin.Log.LogInfo("[NetworkPatches] Skipping ConnectAsClient - direct connect already connected");
                    return false;
                }

                return true; // Run original
            }
        }

        /// <summary>
        /// Patch NetworkConnector.StartLobby to skip Steam lobby when using direct connect.
        ///
        /// Original code (from decompilation):
        ///   if (!instance.forcedOffline && instance.lobbyConnector != null)
        ///   {
        ///       instance.lobbyConnector.StartLobby();
        ///   }
        /// </summary>
        [HarmonyPatch]
        public static class NetworkConnectorStartLobbyPatch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("NetworkConnector");
                return type?.GetMethod("StartLobby", BindingFlags.Static | BindingFlags.Public);
            }

            static bool Prefix()
            {
                if (DirectConnectManager.IsDirectConnectActive)
                {
                    Plugin.Log.LogInfo("[NetworkPatches] Skipping StartLobby - using direct connect");
                    return false; // Skip Steam lobby creation
                }

                return true; // Run original
            }
        }

        /// <summary>
        /// Patch NetworkConnector.Disconnect to handle direct connect cleanup.
        /// </summary>
        [HarmonyPatch]
        public static class NetworkConnectorDisconnectPatch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("NetworkConnector");
                return type?.GetMethod("Disconnect", BindingFlags.Static | BindingFlags.Public);
            }

            static void Postfix()
            {
                if (DirectConnectManager.IsDirectConnectActive)
                {
                    Plugin.Log.LogInfo("[NetworkPatches] Disconnect called - restoring original transport");
                    DirectConnectManager.DisableDirectConnect();
                }
            }
        }
    }

    /// <summary>
    /// Patches for SteamLobbyConnector to bypass Steam when using direct connect.
    /// </summary>
    public static class SteamLobbyPatches
    {
        /// <summary>
        /// Patch SteamLobbyConnector.Awake to skip Steam initialization in direct connect mode.
        ///
        /// Original code checks SteamClient.IsValid and subscribes to Steam events.
        /// </summary>
        [HarmonyPatch]
        public static class SteamLobbyConnectorAwakePatch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("SteamLobbyConnector");
                return type?.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public);
            }

            static void Postfix()
            {
                Plugin.Log.LogInfo("[SteamLobbyPatches] SteamLobbyConnector.Awake completed");
            }
        }

        /// <summary>
        /// Patch SteamLobbyConnector.StartLobby to skip in direct connect mode.
        ///
        /// Original code (from decompilation):
        ///   SteamMatchmaking.CreateLobbyAsync(4);
        /// </summary>
        [HarmonyPatch]
        public static class SteamLobbyConnectorStartLobbyPatch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("SteamLobbyConnector");
                return type?.GetMethod("StartLobby", BindingFlags.Instance | BindingFlags.Public);
            }

            static bool Prefix()
            {
                if (DirectConnectManager.IsDirectConnectActive)
                {
                    Plugin.Log.LogInfo("[SteamLobbyPatches] Skipping SteamLobbyConnector.StartLobby");
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Patch SteamLobbyConnector.JoinLobby to handle direct connect.
        ///
        /// Original code (from decompilation):
        ///   steamLobby.Join();
        /// </summary>
        [HarmonyPatch]
        public static class SteamLobbyConnectorJoinLobbyPatch
        {
            static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("SteamLobbyConnector");
                return type?.GetMethod("JoinLobby", BindingFlags.Instance | BindingFlags.Public);
            }

            static bool Prefix()
            {
                if (DirectConnectManager.IsDirectConnectActive)
                {
                    Plugin.Log.LogInfo("[SteamLobbyPatches] Skipping SteamLobbyConnector.JoinLobby");
                    return false;
                }

                return true;
            }
        }
    }
}
