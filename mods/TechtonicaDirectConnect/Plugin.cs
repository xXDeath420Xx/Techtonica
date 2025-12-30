using System;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mirror;
using kcp2k;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TechtonicaDirectConnect
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Techtonica.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log { get; private set; }
        public static ConfigEntry<int> DefaultPort { get; private set; }
        public static ConfigEntry<string> LastServerAddress { get; private set; }
        public static ConfigEntry<KeyCode> ConnectHotkey { get; private set; }

        private static Plugin _instance;
        public static Plugin Instance => _instance;
        private Harmony _harmony;
        private bool _showConnectUI = false;
        private string _serverAddress = "";
        private string _serverPort = "6968";
        private string _statusMessage = "";
        private bool _isConnecting = false;
        private GUIStyle _windowStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _textFieldStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _statusStyle;
        private Rect _windowRect = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 100, 400, 200);

        private static KcpTransport _kcpTransport;
        private static Transport _originalTransport;
        private static bool _isDirectConnectActive;

        // Windows API for keyboard input (bypasses Rewired)
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_F11 = 0x7A;
        private const int VK_ESCAPE = 0x1B;

        private bool _f11WasPressed = false;
        private bool _escWasPressed = false;
        private float _debugTimer = 0f;
        private bool _updateRunning = false;
        private int _lastToggleFrame = -1; // Prevent double-toggle in same frame

        private void Awake()
        {
            _instance = this;
            Log = Logger;

            // Make this object persist and be hidden
            this.gameObject.hideFlags = HideFlags.HideAndDontSave;

            // Config
            DefaultPort = Config.Bind("General", "DefaultPort", 6968, "Default server port");
            LastServerAddress = Config.Bind("General", "LastServerAddress", "", "Last connected server address");
            ConnectHotkey = Config.Bind("General", "ConnectHotkey", KeyCode.F11, "Hotkey to open connect dialog");

            // Load last server
            _serverAddress = LastServerAddress.Value;
            _serverPort = DefaultPort.Value.ToString();

            // Apply Harmony patches
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Apply null safety patches for networked player objects
            NullSafetyPatches.ApplyPatches(_harmony);

            Log.LogInfo($"[{PluginInfo.PLUGIN_NAME}] v{PluginInfo.PLUGIN_VERSION} loaded!");
            Log.LogInfo($"[{PluginInfo.PLUGIN_NAME}] Press F11 to open connect dialog");
        }

        // Update runs directly on the plugin (like ConsoleCommands mod)
        private void Update()
        {
            // Log once to confirm Update is running
            if (!_updateRunning)
            {
                _updateRunning = true;
                Log.LogInfo("[DirectConnect] Update() is running!");
            }

            // Try Unity's Input first (works for some keys even with Rewired)
            bool f11Unity = Input.GetKeyDown(KeyCode.F11);
            bool escUnity = Input.GetKeyDown(KeyCode.Escape);

            // Also try Windows API as backup
            bool f11Win = (GetAsyncKeyState(VK_F11) & 0x8000) != 0;
            bool escWin = (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0;

            // Debug: log every 5 seconds
            _debugTimer += Time.deltaTime;
            if (_debugTimer > 5f)
            {
                _debugTimer = 0f;
                Log.LogInfo($"[DirectConnect] F11: Unity={f11Unity}, Win={f11Win}, UI={_showConnectUI}");
            }

            // F11 toggle - try both methods (but skip if OnGUI already handled this frame)
            bool f11Triggered = f11Unity || (f11Win && !_f11WasPressed);
            if (f11Triggered && Time.frameCount != _lastToggleFrame)
            {
                _lastToggleFrame = Time.frameCount;
                _showConnectUI = !_showConnectUI;
                if (_showConnectUI)
                {
                    _statusMessage = "";
                }
                Log.LogInfo($"[DirectConnect] UI toggled via Update: {_showConnectUI}");
            }
            _f11WasPressed = f11Win;

            // ESC to close
            bool escTriggered = escUnity || (escWin && !_escWasPressed);
            if (_showConnectUI && escTriggered)
            {
                _showConnectUI = false;
                Log.LogInfo("[DirectConnect] UI closed via ESC");
            }
            _escWasPressed = escWin;
        }

        // OnGUI runs directly on the plugin
        private void OnGUI()
        {
            // Check for F11 via Event.current (only toggle once per frame)
            var e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.F11)
            {
                // Prevent multiple toggles in the same frame (OnGUI is called multiple times)
                if (Time.frameCount != _lastToggleFrame)
                {
                    _lastToggleFrame = Time.frameCount;
                    _showConnectUI = !_showConnectUI;
                    if (_showConnectUI) _statusMessage = "";
                    Log.LogInfo($"[DirectConnect] UI toggled via Event.current: {_showConnectUI}");
                }
                e.Use();
            }

            if (!_showConnectUI) return;

            InitStyles();

            _windowRect = GUI.Window(12345, _windowRect, DrawConnectWindow, "Direct Connect", _windowStyle);
        }

        private void InitStyles()
        {
            if (_windowStyle != null) return;

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = MakeTexture(2, 2, new Color(0.1f, 0.1f, 0.15f, 0.95f));
            _windowStyle.fontSize = 16;
            _windowStyle.fontStyle = FontStyle.Bold;
            _windowStyle.normal.textColor = new Color(0.65f, 0.55f, 0.98f);
            _windowStyle.padding = new RectOffset(15, 15, 25, 15);

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.fontSize = 14;
            _labelStyle.normal.textColor = Color.white;

            _textFieldStyle = new GUIStyle(GUI.skin.textField);
            _textFieldStyle.fontSize = 14;
            _textFieldStyle.normal.background = MakeTexture(2, 2, new Color(0.15f, 0.15f, 0.2f, 1f));
            _textFieldStyle.normal.textColor = Color.white;
            _textFieldStyle.padding = new RectOffset(10, 10, 8, 8);

            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = 14;
            _buttonStyle.fontStyle = FontStyle.Bold;
            _buttonStyle.normal.background = MakeTexture(2, 2, new Color(0.49f, 0.23f, 0.93f, 1f));
            _buttonStyle.hover.background = MakeTexture(2, 2, new Color(0.59f, 0.33f, 1f, 1f));
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.hover.textColor = Color.white;
            _buttonStyle.padding = new RectOffset(15, 15, 10, 10);

            _statusStyle = new GUIStyle(GUI.skin.label);
            _statusStyle.fontSize = 12;
            _statusStyle.alignment = TextAnchor.MiddleCenter;
            _statusStyle.wordWrap = true;
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            Texture2D tex = new Texture2D(width, height);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private void DrawConnectWindow(int windowID)
        {
            GUILayout.Space(10);

            // Server Address
            GUILayout.BeginHorizontal();
            GUILayout.Label("Server:", _labelStyle, GUILayout.Width(60));
            _serverAddress = GUILayout.TextField(_serverAddress, _textFieldStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Port
            GUILayout.BeginHorizontal();
            GUILayout.Label("Port:", _labelStyle, GUILayout.Width(60));
            _serverPort = GUILayout.TextField(_serverPort, _textFieldStyle, GUILayout.Width(80));
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            // Connection status
            bool isConnected = NetworkClient.active;
            if (isConnected)
            {
                _statusStyle.normal.textColor = new Color(0.2f, 0.83f, 0.6f);
                GUILayout.Label($"Connected to {NetworkManager.singleton?.networkAddress}", _statusStyle);

                GUILayout.Space(10);

                if (GUILayout.Button("Disconnect", _buttonStyle))
                {
                    Disconnect();
                }
            }
            else
            {
                // Status message
                if (!string.IsNullOrEmpty(_statusMessage))
                {
                    _statusStyle.normal.textColor = _statusMessage.Contains("Error") || _statusMessage.Contains("Failed")
                        ? new Color(0.97f, 0.44f, 0.44f)
                        : new Color(0.65f, 0.55f, 0.98f);
                    GUILayout.Label(_statusMessage, _statusStyle);
                    GUILayout.Space(5);
                }

                // Connect button
                GUI.enabled = !_isConnecting && !string.IsNullOrWhiteSpace(_serverAddress);
                if (GUILayout.Button(_isConnecting ? "Connecting..." : "Connect", _buttonStyle))
                {
                    Connect();
                }
                GUI.enabled = true;
            }

            GUILayout.Space(10);

            // Close button
            var closeStyle = new GUIStyle(_buttonStyle);
            closeStyle.normal.background = MakeTexture(2, 2, new Color(0.3f, 0.3f, 0.35f, 1f));
            if (GUILayout.Button("Close", closeStyle))
            {
                _showConnectUI = false;
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 30));
        }

        private void Connect()
        {
            if (string.IsNullOrWhiteSpace(_serverAddress))
            {
                _statusMessage = "Please enter a server address";
                return;
            }

            if (!int.TryParse(_serverPort, out int port) || port < 1 || port > 65535)
            {
                _statusMessage = "Invalid port number";
                return;
            }

            try
            {
                // Check if we're in a valid game scene (not main menu)
                var currentScene = SceneManager.GetActiveScene().name;
                Log.LogInfo($"[DirectConnect] Current scene: {currentScene}");

                // Log all loaded scenes
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    Log.LogInfo($"[DirectConnect] Loaded scene {i}: {scene.name} (active: {scene == SceneManager.GetActiveScene()})");
                }

                // Check if we're in the game world - look for Player Scene or Voxeland Scene
                bool inGameWorld = false;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sceneName = SceneManager.GetSceneAt(i).name;
                    if (sceneName.Contains("Player") || sceneName.Contains("Voxeland"))
                    {
                        inGameWorld = true;
                        break;
                    }
                }

                if (!inGameWorld)
                {
                    Log.LogWarning("[DirectConnect] Not in game world! Trying to trigger JoinMultiplayerAsClient...");
                    _statusMessage = "Loading game world...";

                    // Try to call the game's JoinMultiplayerAsClient via reflection
                    var networkConnectorType = AccessTools.TypeByName("NetworkConnector");
                    if (networkConnectorType != null)
                    {
                        var joinMethod = AccessTools.Method(networkConnectorType, "JoinMultiplayerAsClient");
                        if (joinMethod != null)
                        {
                            Log.LogInfo("[DirectConnect] Found JoinMultiplayerAsClient, invoking...");
                            joinMethod.Invoke(null, null);

                            // Wait for scene to load then connect
                            _pendingConnect = true;
                            _pendingPort = port;
                            StartCoroutine(WaitForSceneAndConnect(port));
                            return;
                        }
                        else
                        {
                            Log.LogWarning("[DirectConnect] JoinMultiplayerAsClient method not found");
                        }
                    }

                    // If we can't trigger join, warn user
                    _statusMessage = "Start/load a game first, then connect";
                    Log.LogWarning("[DirectConnect] Please load a game before connecting to a server");
                    return;
                }

                _isConnecting = true;
                _statusMessage = "Connecting...";

                // Save last server
                LastServerAddress.Value = _serverAddress;

                // Enable direct connect transport
                if (!EnableDirectConnect(port))
                {
                    _statusMessage = "Failed to initialize connection";
                    _isConnecting = false;
                    return;
                }

                var networkManager = NetworkManager.singleton;
                if (networkManager == null)
                {
                    _statusMessage = "Error: NetworkManager not found";
                    _isConnecting = false;
                    return;
                }

                // Set address
                networkManager.networkAddress = _serverAddress;

                // Start client
                networkManager.StartClient();
                Log.LogInfo($"[DirectConnect] Connecting to {_serverAddress}:{port}...");

                // Auto-close on success after delay
                StartCoroutine(CheckConnection());
            }
            catch (Exception ex)
            {
                _statusMessage = $"Error: {ex.Message}";
                _isConnecting = false;
                Log.LogError($"[DirectConnect] Connection error: {ex}");
            }
        }

        private bool _pendingConnect = false;
        private int _pendingPort = 0;

        private System.Collections.IEnumerator WaitForSceneAndConnect(int port)
        {
            float timeout = 30f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                // Check if game scenes are loaded
                bool inGameWorld = false;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sceneName = SceneManager.GetSceneAt(i).name;
                    if (sceneName.Contains("Player") || sceneName.Contains("Voxeland"))
                    {
                        inGameWorld = true;
                        break;
                    }
                }

                if (inGameWorld)
                {
                    Log.LogInfo("[DirectConnect] Game world loaded! Proceeding with connection...");
                    _pendingConnect = false;

                    // Small delay to let things settle
                    yield return new WaitForSeconds(1f);

                    // Now connect
                    _isConnecting = true;
                    _statusMessage = "Connecting...";
                    LastServerAddress.Value = _serverAddress;

                    if (!EnableDirectConnect(port))
                    {
                        _statusMessage = "Failed to initialize connection";
                        _isConnecting = false;
                        yield break;
                    }

                    var networkManager = NetworkManager.singleton;
                    if (networkManager != null)
                    {
                        networkManager.networkAddress = _serverAddress;
                        networkManager.StartClient();
                        Log.LogInfo($"[DirectConnect] Connecting to {_serverAddress}:{port}...");
                        StartCoroutine(CheckConnection());
                    }
                    yield break;
                }

                elapsed += 0.5f;
                _statusMessage = $"Loading game world... {elapsed:F0}s";
                yield return new WaitForSeconds(0.5f);
            }

            _statusMessage = "Failed to load game world";
            _pendingConnect = false;
            Log.LogError("[DirectConnect] Timed out waiting for game world to load");
        }

        private System.Collections.IEnumerator CheckConnection()
        {
            float timeout = 15f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (NetworkClient.isConnected)
                {
                    Log.LogInfo("[DirectConnect] Connected! Sending ready signal...");
                    _statusMessage = "Connected! Joining game...";

                    // Tell the server we're ready to receive spawned objects
                    if (!NetworkClient.ready)
                    {
                        try
                        {
                            NetworkClient.Ready();
                            Log.LogInfo("[DirectConnect] Sent Ready signal");
                        }
                        catch (Exception ex)
                        {
                            Log.LogWarning($"[DirectConnect] Ready failed: {ex.Message}");
                        }
                    }

                    // Wait a moment for server to process
                    yield return new WaitForSeconds(0.5f);

                    // Request player spawning if available
                    try
                    {
                        if (NetworkClient.ready && NetworkClient.localPlayer == null)
                        {
                            // Try to add player - this triggers server-side player spawning
                            NetworkClient.AddPlayer();
                            Log.LogInfo("[DirectConnect] Requested player spawn");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"[DirectConnect] AddPlayer failed: {ex.Message}");
                    }

                    _statusMessage = "Connected!";
                    _isConnecting = false;
                    yield return new WaitForSeconds(1f);
                    _showConnectUI = false;
                    yield break;
                }

                if (!NetworkClient.active && !_isConnecting)
                {
                    _statusMessage = "Connection failed";
                    yield break;
                }

                elapsed += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            if (!NetworkClient.isConnected)
            {
                _statusMessage = "Connection timed out";
                _isConnecting = false;
                NetworkManager.singleton?.StopClient();
            }
        }

        private void Disconnect()
        {
            try
            {
                NetworkManager.singleton?.StopClient();
                DisableDirectConnect();
                _statusMessage = "Disconnected";
                Log.LogInfo("[DirectConnect] Disconnected");
            }
            catch (Exception ex)
            {
                Log.LogError($"[DirectConnect] Disconnect error: {ex}");
            }
        }

        private static bool EnableDirectConnect(int port)
        {
            try
            {
                var networkManager = NetworkManager.singleton;
                if (networkManager == null)
                {
                    Log.LogError("[DirectConnect] NetworkManager not found!");
                    return false;
                }

                // Get transport field via reflection
                var transportField = typeof(NetworkManager).GetField("transport",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (transportField == null)
                {
                    Log.LogError("[DirectConnect] Could not find transport field!");
                    return false;
                }

                // Store original transport
                _originalTransport = transportField.GetValue(networkManager) as Transport;

                // Create KCP transport if needed
                if (_kcpTransport == null)
                {
                    var transportGO = new GameObject("DirectConnect_KcpTransport");
                    DontDestroyOnLoad(transportGO);
                    _kcpTransport = transportGO.AddComponent<KcpTransport>();
                }

                // Configure KCP
                _kcpTransport.Port = (ushort)port;
                _kcpTransport.NoDelay = true;
                _kcpTransport.Interval = 10;
                _kcpTransport.Timeout = 10000;

                // Swap transport
                transportField.SetValue(networkManager, _kcpTransport);

                // Set active transport
                var activeTransportField = typeof(Transport).GetField("activeTransport",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                activeTransportField?.SetValue(null, _kcpTransport);

                _isDirectConnectActive = true;
                Log.LogInfo($"[DirectConnect] Switched to KCP transport on port {port}");

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"[DirectConnect] Failed to enable: {ex}");
                return false;
            }
        }

        private static void DisableDirectConnect()
        {
            if (_originalTransport != null && NetworkManager.singleton != null)
            {
                try
                {
                    var transportField = typeof(NetworkManager).GetField("transport",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    transportField?.SetValue(NetworkManager.singleton, _originalTransport);

                    var activeTransportField = typeof(Transport).GetField("activeTransport",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    activeTransportField?.SetValue(null, _originalTransport);

                    _isDirectConnectActive = false;
                    Log.LogInfo("[DirectConnect] Restored original transport");
                }
                catch (Exception ex)
                {
                    Log.LogError($"[DirectConnect] Failed to restore transport: {ex}");
                }
            }
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            if (_kcpTransport != null)
            {
                Destroy(_kcpTransport.gameObject);
            }
        }
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.certifried.techtonicadirectconnect";
        public const string PLUGIN_NAME = "Techtonica Direct Connect";
        public const string PLUGIN_VERSION = "1.0.18";
    }

    /// <summary>
    /// Patches to prevent null reference exceptions when connecting to dedicated servers.
    /// These errors occur because the game expects local player references that don't exist
    /// in the multiplayer context until fully joined.
    /// </summary>
    public static class NullSafetyPatches
    {
        private static bool _patchesApplied = false;

        public static void ApplyPatches(Harmony harmony)
        {
            if (_patchesApplied) return;

            try
            {
                // Patch NetworkedPlayer methods
                var networkedPlayerType = AccessTools.TypeByName("NetworkedPlayer");
                if (networkedPlayerType != null)
                {
                    var prefix = new HarmonyMethod(typeof(NullSafetyPatches), nameof(Skip_Prefix));

                    // Patch Update
                    var updateMethod = AccessTools.Method(networkedPlayerType, "Update");
                    if (updateMethod != null)
                    {
                        harmony.Patch(updateMethod, prefix: prefix);
                        Plugin.Log.LogInfo("[DirectConnect] Patched NetworkedPlayer.Update to skip");
                    }

                    // Patch OnStartClient - crashes when player spawns without proper initialization
                    var onStartClientMethod = AccessTools.Method(networkedPlayerType, "OnStartClient");
                    if (onStartClientMethod != null)
                    {
                        harmony.Patch(onStartClientMethod, prefix: prefix);
                        Plugin.Log.LogInfo("[DirectConnect] Patched NetworkedPlayer.OnStartClient to skip");
                    }

                    // Patch OnStartLocalPlayer - crashes when local player created without proper scene
                    var onStartLocalPlayerMethod = AccessTools.Method(networkedPlayerType, "OnStartLocalPlayer");
                    if (onStartLocalPlayerMethod != null)
                    {
                        harmony.Patch(onStartLocalPlayerMethod, prefix: prefix);
                        Plugin.Log.LogInfo("[DirectConnect] Patched NetworkedPlayer.OnStartLocalPlayer to skip");
                    }
                }
                else
                {
                    Plugin.Log.LogWarning("[DirectConnect] NetworkedPlayer type not found");
                }

                // Patch ThirdPersonDisplayAnimator.Update and UpdateSillyStuff
                var animatorType = AccessTools.TypeByName("ThirdPersonDisplayAnimator");
                if (animatorType != null)
                {
                    var updateMethod = AccessTools.Method(animatorType, "Update");
                    if (updateMethod != null)
                    {
                        var prefix = new HarmonyMethod(typeof(NullSafetyPatches), nameof(Skip_Prefix));
                        harmony.Patch(updateMethod, prefix: prefix);
                        Plugin.Log.LogInfo("[DirectConnect] Patched ThirdPersonDisplayAnimator.Update to skip");
                    }

                    var sillyMethod = AccessTools.Method(animatorType, "UpdateSillyStuff");
                    if (sillyMethod != null)
                    {
                        var prefix = new HarmonyMethod(typeof(NullSafetyPatches), nameof(Skip_Prefix));
                        harmony.Patch(sillyMethod, prefix: prefix);
                        Plugin.Log.LogInfo("[DirectConnect] Patched ThirdPersonDisplayAnimator.UpdateSillyStuff to skip");
                    }
                }
                else
                {
                    Plugin.Log.LogWarning("[DirectConnect] ThirdPersonDisplayAnimator type not found");
                }

                _patchesApplied = true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[DirectConnect] Failed to apply null safety patches: {ex}");
            }
        }

        /// <summary>
        /// Prefix that skips the original method entirely
        /// </summary>
        public static bool Skip_Prefix()
        {
            return false;
        }
    }
}
