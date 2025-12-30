using System;
using System.Collections.Generic;
using TechtonicaDedicatedServer.Networking;
using UnityEngine;

namespace TechtonicaDedicatedServer
{
    /// <summary>
    /// Console commands for dedicated server management.
    /// All commands are prefixed with "ds." (dedicated server).
    /// </summary>
    public static class ConsoleCommands
    {
        private static readonly Dictionary<string, Action<string[]>> Commands = new Dictionary<string, Action<string[]>>();
        private static bool _isRegistered;

        public static void Register()
        {
            if (_isRegistered) return;

            // Register commands
            Commands["ds.help"] = CmdHelp;
            Commands["ds.host"] = CmdHost;
            Commands["ds.server"] = CmdServer;
            Commands["ds.connect"] = CmdConnect;
            Commands["ds.disconnect"] = CmdDisconnect;
            Commands["ds.stop"] = CmdStop;
            Commands["ds.status"] = CmdStatus;
            Commands["ds.players"] = CmdPlayers;
            Commands["ds.kick"] = CmdKick;
            Commands["ds.say"] = CmdSay;

            _isRegistered = true;

            // Try to hook into the game's console system
            HookGameConsole();

            Plugin.Log.LogInfo("[Commands] Registered console commands");
        }

        private static void HookGameConsole()
        {
            // Try to find and hook the game's console command system
            // This will be done via reflection since we don't have direct access

            try
            {
                // Look for a console manager or command handler
                var consoleType = AccessTools.TypeByName("ConsoleCommands") ??
                                 AccessTools.TypeByName("DevConsole") ??
                                 AccessTools.TypeByName("CommandHandler");

                if (consoleType != null)
                {
                    Plugin.Log.LogInfo($"[Commands] Found console type: {consoleType.Name}");
                    // Hook into it if possible
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[Commands] Could not hook game console: {ex.Message}");
            }
        }

        /// <summary>
        /// Process a command string.
        /// </summary>
        public static bool ProcessCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;

            var parts = input.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            var cmd = parts[0].ToLower();

            if (Commands.TryGetValue(cmd, out var handler))
            {
                var args = new string[parts.Length - 1];
                Array.Copy(parts, 1, args, 0, args.Length);

                try
                {
                    handler(args);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"[Commands] Error executing {cmd}: {ex}");
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Process input in headless mode (stdin).
        /// </summary>
        public static void ProcessHeadlessInput()
        {
            // In headless mode, we might read from stdin
            // This is a simple implementation - could be enhanced with a proper console

            if (!Plugin.HeadlessMode.Value) return;

            // Note: Unity's console input handling is limited
            // For full headless support, we'd need to use a separate thread
            // reading from Console.In
        }

        // ==================== Command Handlers ====================

        private static void CmdHelp(string[] args)
        {
            Plugin.Log.LogInfo("=== Techtonica Dedicated Server Commands ===");
            Plugin.Log.LogInfo("ds.help              - Show this help");
            Plugin.Log.LogInfo("ds.host [port]       - Start as host (server + local player)");
            Plugin.Log.LogInfo("ds.server [port]     - Start dedicated server only");
            Plugin.Log.LogInfo("ds.connect <ip:port> - Connect to a server");
            Plugin.Log.LogInfo("ds.disconnect        - Disconnect from server");
            Plugin.Log.LogInfo("ds.stop              - Stop server/disconnect");
            Plugin.Log.LogInfo("ds.status            - Show server status");
            Plugin.Log.LogInfo("ds.players           - List connected players");
            Plugin.Log.LogInfo("ds.kick <player>     - Kick a player");
            Plugin.Log.LogInfo("ds.say <message>     - Broadcast a message");
        }

        private static void CmdHost(string[] args)
        {
            int port = -1;
            if (args.Length > 0 && int.TryParse(args[0], out var parsedPort))
            {
                port = parsedPort;
            }

            if (DirectConnectManager.StartHost(port))
            {
                var addr = DirectConnectManager.GetServerAddress();
                Plugin.Log.LogInfo($"Host started! Players can connect to: {addr}");
            }
            else
            {
                Plugin.Log.LogError("Failed to start host");
            }
        }

        private static void CmdServer(string[] args)
        {
            int port = -1;
            if (args.Length > 0 && int.TryParse(args[0], out var parsedPort))
            {
                port = parsedPort;
            }

            if (DirectConnectManager.StartServer(port))
            {
                var addr = DirectConnectManager.GetServerAddress();
                Plugin.Log.LogInfo($"Server started! Players can connect to: {addr}");
            }
            else
            {
                Plugin.Log.LogError("Failed to start server");
            }
        }

        private static void CmdConnect(string[] args)
        {
            if (args.Length == 0)
            {
                Plugin.Log.LogError("Usage: ds.connect <ip:port>");
                return;
            }

            var address = args[0];
            if (DirectConnectManager.Connect(address))
            {
                Plugin.Log.LogInfo($"Connecting to {address}...");
            }
            else
            {
                Plugin.Log.LogError($"Failed to connect to {address}");
            }
        }

        private static void CmdDisconnect(string[] args)
        {
            DirectConnectManager.Stop();
            Plugin.Log.LogInfo("Disconnected");
        }

        private static void CmdStop(string[] args)
        {
            DirectConnectManager.Stop();
            Plugin.Log.LogInfo("Stopped");
        }

        private static void CmdStatus(string[] args)
        {
            Plugin.Log.LogInfo("=== Server Status ===");
            Plugin.Log.LogInfo($"Direct Connect Active: {DirectConnectManager.IsDirectConnectActive}");
            Plugin.Log.LogInfo($"Is Server: {DirectConnectManager.IsServer}");
            Plugin.Log.LogInfo($"Is Client: {DirectConnectManager.IsClient}");

            if (DirectConnectManager.IsServer)
            {
                Plugin.Log.LogInfo($"Server Address: {DirectConnectManager.GetServerAddress()}");
                Plugin.Log.LogInfo($"Max Players: {Plugin.MaxPlayers.Value}");

                var nm = Mirror.NetworkManager.singleton;
                if (nm != null)
                {
                    Plugin.Log.LogInfo($"Connected Clients: {nm.numPlayers}");
                }
            }
        }

        private static void CmdPlayers(string[] args)
        {
            if (!DirectConnectManager.IsServer)
            {
                Plugin.Log.LogError("Not running as server");
                return;
            }

            Plugin.Log.LogInfo("=== Connected Players ===");

            var nm = Mirror.NetworkManager.singleton;
            if (nm == null)
            {
                Plugin.Log.LogError("NetworkManager not available");
                return;
            }

            foreach (var conn in Mirror.NetworkServer.connections)
            {
                Plugin.Log.LogInfo($"  [{conn.Key}] {conn.Value.address}");
            }
        }

        private static void CmdKick(string[] args)
        {
            if (!DirectConnectManager.IsServer)
            {
                Plugin.Log.LogError("Not running as server");
                return;
            }

            if (args.Length == 0)
            {
                Plugin.Log.LogError("Usage: ds.kick <connectionId>");
                return;
            }

            if (!int.TryParse(args[0], out var connId))
            {
                Plugin.Log.LogError("Invalid connection ID");
                return;
            }

            if (Mirror.NetworkServer.connections.TryGetValue(connId, out var conn))
            {
                conn.Disconnect();
                Plugin.Log.LogInfo($"Kicked connection {connId}");
            }
            else
            {
                Plugin.Log.LogError($"Connection {connId} not found");
            }
        }

        private static void CmdSay(string[] args)
        {
            if (args.Length == 0)
            {
                Plugin.Log.LogError("Usage: ds.say <message>");
                return;
            }

            var message = string.Join(" ", args);
            Plugin.Log.LogInfo($"[Server] {message}");

            // TODO: Broadcast to game chat when chat system is hooked
        }
    }

    /// <summary>
    /// Helper class for accessing non-public types via reflection.
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

                    // Also try with common namespaces
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

        public static System.Reflection.MethodInfo Method(Type type, string name)
        {
            if (type == null) return null;
            return type.GetMethod(name,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
        }
    }
}
