using System;
using System.Net;
using System.Reflection;
using Mirror;
using UnityEngine;
using kcp2k;

namespace TechtonicaDedicatedServer.Networking
{
    /// <summary>
    /// Manages direct IP connections by swapping the transport layer from FizzyFacepunch to KCP.
    /// Based on decompilation of TechNetworkManager, NetworkConnector, and SteamLobbyConnector.
    /// </summary>
    public static class DirectConnectManager
    {
        private static KcpTransport _kcpTransport;
        private static Transport _originalTransport;
        private static bool _isInitialized;
        private static bool _isDirectConnectActive;
        private static DirectConnectLobbyConnector _directConnectLobby;

        public static bool IsDirectConnectActive => _isDirectConnectActive;
        public static bool IsServer => NetworkServer.active;
        public static bool IsClient => NetworkClient.active;
        public static bool IsHost => NetworkServer.active && NetworkClient.active;

        public static event Action OnServerStarted;
        public static event Action OnServerStopped;
        public static event Action OnClientConnected;
        public static event Action OnClientDisconnected;

        public static void Initialize()
        {
            if (_isInitialized) return;

            Plugin.Log.LogInfo("[DirectConnect] Initializing...");
            _isInitialized = true;
            Plugin.Log.LogInfo("[DirectConnect] Initialized successfully");
        }

        public static void Cleanup()
        {
            if (_kcpTransport != null)
            {
                GameObject.Destroy(_kcpTransport.gameObject);
                _kcpTransport = null;
            }

            _isInitialized = false;
            _isDirectConnectActive = false;
        }

        /// <summary>
        /// Creates or retrieves the KCP transport for direct connections.
        /// </summary>
        public static KcpTransport GetOrCreateKcpTransport()
        {
            if (_kcpTransport != null) return _kcpTransport;

            // Create a new GameObject for our transport
            var transportGO = new GameObject("DirectConnect_KcpTransport");
            GameObject.DontDestroyOnLoad(transportGO);

            _kcpTransport = transportGO.AddComponent<KcpTransport>();

            // Configure KCP for game traffic
            _kcpTransport.Port = (ushort)Plugin.ServerPort.Value;
            _kcpTransport.NoDelay = true;
            _kcpTransport.Interval = 10;
            _kcpTransport.Timeout = 10000;

            Plugin.Log.LogInfo($"[DirectConnect] Created KCP transport on port {_kcpTransport.Port}");

            return _kcpTransport;
        }

        /// <summary>
        /// Switches the active transport from FizzyFacepunch to KCP.
        /// Must be called BEFORE starting server/client.
        /// </summary>
        public static bool EnableDirectConnect()
        {
            if (!Plugin.EnableDirectConnect.Value)
            {
                Plugin.Log.LogWarning("[DirectConnect] Direct connect is disabled in config");
                return false;
            }

            try
            {
                var networkManager = NetworkManager.singleton;
                if (networkManager == null)
                {
                    Plugin.Log.LogError("[DirectConnect] NetworkManager not found!");
                    return false;
                }

                // Get the transport field via reflection (it's internal in some Mirror versions)
                var transportField = typeof(NetworkManager).GetField("transport",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (transportField == null)
                {
                    Plugin.Log.LogError("[DirectConnect] Could not find transport field!");
                    return false;
                }

                // Store the original transport
                _originalTransport = transportField.GetValue(networkManager) as Transport;

                // Get or create our KCP transport
                var kcpTransport = GetOrCreateKcpTransport();

                // Swap the transport on NetworkManager using reflection
                transportField.SetValue(networkManager, kcpTransport);

                // Also try to set Transport.activeTransport if it exists
                var activeTransportField = typeof(Transport).GetField("activeTransport",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (activeTransportField != null)
                {
                    activeTransportField.SetValue(null, kcpTransport);
                }

                _isDirectConnectActive = true;
                Plugin.Log.LogInfo("[DirectConnect] Switched to KCP transport");

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[DirectConnect] Failed to switch transport: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Restores the original FizzyFacepunch transport.
        /// </summary>
        public static void DisableDirectConnect()
        {
            if (_originalTransport != null && NetworkManager.singleton != null)
            {
                try
                {
                    // Use reflection to set the transport (it's internal in some Mirror versions)
                    var transportField = typeof(NetworkManager).GetField("transport",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (transportField != null)
                    {
                        transportField.SetValue(NetworkManager.singleton, _originalTransport);
                    }

                    // Also try to set Transport.activeTransport if it exists
                    var activeTransportField = typeof(Transport).GetField("activeTransport",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (activeTransportField != null)
                    {
                        activeTransportField.SetValue(null, _originalTransport);
                    }

                    _isDirectConnectActive = false;
                    Plugin.Log.LogInfo("[DirectConnect] Restored original transport");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[DirectConnect] Failed to restore transport: {ex}");
                }
            }
        }

        /// <summary>
        /// Starts a dedicated server (no local client).
        /// Based on NetworkConnector.ConnectAsHost() but without starting a host.
        /// </summary>
        public static bool StartServer(int port = -1)
        {
            if (IsServer)
            {
                Plugin.Log.LogWarning("[DirectConnect] Server is already running");
                return false;
            }

            try
            {
                if (port > 0)
                {
                    Plugin.ServerPort.Value = port;
                }

                // Enable direct connect transport
                if (!EnableDirectConnect())
                {
                    return false;
                }

                // Update port
                if (_kcpTransport != null)
                {
                    _kcpTransport.Port = (ushort)Plugin.ServerPort.Value;
                }

                var networkManager = NetworkManager.singleton;
                if (networkManager == null)
                {
                    Plugin.Log.LogError("[DirectConnect] NetworkManager not found!");
                    return false;
                }

                networkManager.maxConnections = Plugin.MaxPlayers.Value;

                // Start server only (not host)
                networkManager.StartServer();

                Plugin.Log.LogInfo($"[DirectConnect] Server started on port {Plugin.ServerPort.Value}");
                Plugin.Log.LogInfo($"[DirectConnect] Max players: {Plugin.MaxPlayers.Value}");
                Plugin.Log.LogInfo($"[DirectConnect] Address: {GetServerAddress()}");

                OnServerStarted?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[DirectConnect] Failed to start server: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Starts as host (server + local client).
        /// Based on NetworkConnector.ConnectAsHost() without Steam lobby.
        /// </summary>
        public static bool StartHost(int port = -1)
        {
            if (IsServer)
            {
                Plugin.Log.LogWarning("[DirectConnect] Server is already running");
                return false;
            }

            try
            {
                if (port > 0)
                {
                    Plugin.ServerPort.Value = port;
                }

                // Enable direct connect transport
                if (!EnableDirectConnect())
                {
                    return false;
                }

                // Update port
                if (_kcpTransport != null)
                {
                    _kcpTransport.Port = (ushort)Plugin.ServerPort.Value;
                }

                var networkManager = NetworkManager.singleton;
                if (networkManager == null)
                {
                    Plugin.Log.LogError("[DirectConnect] NetworkManager not found!");
                    return false;
                }

                networkManager.maxConnections = Plugin.MaxPlayers.Value;

                // This mirrors NetworkConnector.ConnectAsHost() but skips StartLobby()
                // From decompilation: NetworkManager.singleton.StartHost();
                networkManager.StartHost();

                Plugin.Log.LogInfo($"[DirectConnect] Host started on port {Plugin.ServerPort.Value}");
                Plugin.Log.LogInfo($"[DirectConnect] Address: {GetServerAddress()}");

                OnServerStarted?.Invoke();
                OnClientConnected?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[DirectConnect] Failed to start host: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Connects to a server at the specified address.
        /// Based on NetworkConnector.ConnectAsClient() with direct IP.
        /// </summary>
        public static bool Connect(string address, int port = -1)
        {
            if (IsClient)
            {
                Plugin.Log.LogWarning("[DirectConnect] Already connected");
                return false;
            }

            try
            {
                // Enable direct connect transport
                if (!EnableDirectConnect())
                {
                    return false;
                }

                var networkManager = NetworkManager.singleton;
                if (networkManager == null)
                {
                    Plugin.Log.LogError("[DirectConnect] NetworkManager not found!");
                    return false;
                }

                // Parse address
                string host = address;
                int targetPort = port > 0 ? port : Plugin.ServerPort.Value;

                // Check if address includes port
                if (address.Contains(":"))
                {
                    var parts = address.Split(':');
                    host = parts[0];
                    if (parts.Length > 1 && int.TryParse(parts[1], out int parsedPort))
                    {
                        targetPort = parsedPort;
                    }
                }

                // Update KCP port
                if (_kcpTransport != null)
                {
                    _kcpTransport.Port = (ushort)targetPort;
                }

                // Set network address (from decompilation: NetworkManager.singleton.networkAddress = text)
                networkManager.networkAddress = host;

                // This mirrors NetworkConnector.ConnectAsClient()
                // From decompilation: NetworkManager.singleton.StartClient();
                networkManager.StartClient();

                Plugin.Log.LogInfo($"[DirectConnect] Connecting to {host}:{targetPort}...");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[DirectConnect] Failed to connect: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Stops the server or disconnects from the current server.
        /// Based on NetworkConnector.Disconnect().
        /// </summary>
        public static void Stop()
        {
            try
            {
                var networkManager = NetworkManager.singleton;
                if (networkManager == null) return;

                // From decompilation of NetworkConnector.Disconnect():
                if (IsHost)
                {
                    networkManager.StopHost();
                    Plugin.Log.LogInfo("[DirectConnect] Host stopped");
                    OnServerStopped?.Invoke();
                }
                else if (IsServer)
                {
                    networkManager.StopServer();
                    Plugin.Log.LogInfo("[DirectConnect] Server stopped");
                    OnServerStopped?.Invoke();
                }
                else if (IsClient)
                {
                    networkManager.StopClient();
                    Plugin.Log.LogInfo("[DirectConnect] Disconnected");
                    OnClientDisconnected?.Invoke();
                }

                // Restore original transport
                DisableDirectConnect();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[DirectConnect] Error stopping: {ex}");
            }
        }

        /// <summary>
        /// Gets the server's network address for players to connect.
        /// </summary>
        public static string GetServerAddress()
        {
            if (!IsServer) return null;

            try
            {
                var hostName = Dns.GetHostName();
                var addresses = Dns.GetHostAddresses(hostName);

                foreach (var addr in addresses)
                {
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        // Skip loopback
                        if (!addr.ToString().StartsWith("127."))
                        {
                            return $"{addr}:{Plugin.ServerPort.Value}";
                        }
                    }
                }

                return $"localhost:{Plugin.ServerPort.Value}";
            }
            catch
            {
                return $"localhost:{Plugin.ServerPort.Value}";
            }
        }

        /// <summary>
        /// Gets the number of connected players.
        /// </summary>
        public static int GetPlayerCount()
        {
            if (!IsServer) return 0;
            return NetworkServer.connections.Count;
        }
    }
}
