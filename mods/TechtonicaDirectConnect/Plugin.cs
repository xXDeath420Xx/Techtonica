using System;
using System.Net;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Mirror;
using kcp2k;
using UnityEngine;

namespace TechtonicaDirectConnect
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log { get; private set; }
        public static ConfigEntry<int> DefaultPort { get; private set; }
        public static ConfigEntry<string> LastServerAddress { get; private set; }
        public static ConfigEntry<KeyCode> ConnectHotkey { get; private set; }

        private static Plugin _instance;
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

        private void Awake()
        {
            _instance = this;
            Log = Logger;

            // Config
            DefaultPort = Config.Bind("General", "DefaultPort", 6968, "Default server port");
            LastServerAddress = Config.Bind("General", "LastServerAddress", "", "Last connected server address");
            ConnectHotkey = Config.Bind("General", "ConnectHotkey", KeyCode.F8, "Hotkey to open connect dialog");

            // Load last server
            _serverAddress = LastServerAddress.Value;
            _serverPort = DefaultPort.Value.ToString();

            // Apply Harmony patches
            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.LogInfo($"[{PluginInfo.PLUGIN_NAME}] v{PluginInfo.PLUGIN_VERSION} loaded!");
            Log.LogInfo($"[{PluginInfo.PLUGIN_NAME}] Press {ConnectHotkey.Value} to open connect dialog");
        }

        private void Update()
        {
            // Toggle connect UI with hotkey
            if (Input.GetKeyDown(ConnectHotkey.Value))
            {
                _showConnectUI = !_showConnectUI;
                if (_showConnectUI)
                {
                    _statusMessage = "";
                }
            }

            // ESC to close
            if (_showConnectUI && Input.GetKeyDown(KeyCode.Escape))
            {
                _showConnectUI = false;
            }
        }

        private void OnGUI()
        {
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

        private System.Collections.IEnumerator CheckConnection()
        {
            float timeout = 10f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (NetworkClient.isConnected)
                {
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
        public const string PLUGIN_VERSION = "1.0.0";
    }
}
