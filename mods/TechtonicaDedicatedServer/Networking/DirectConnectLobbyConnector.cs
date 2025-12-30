using System;
using System.Collections.Generic;

namespace TechtonicaDedicatedServer.Networking
{
    /// <summary>
    /// A LobbyConnector implementation that bypasses Steam lobbies for direct IP connections.
    /// Implements the same interface as SteamLobbyConnector but does nothing Steam-specific.
    /// </summary>
    public class DirectConnectLobbyConnector : LobbyConnector
    {
        private bool _isInLobby;
        private string _currentAddress;
        private int _currentPort;

        public string CurrentAddress => _currentAddress;
        public int CurrentPort => _currentPort;

        public void SetConnectionTarget(string address, int port)
        {
            _currentAddress = address;
            _currentPort = port;
        }

        public void Awake()
        {
            Plugin.Log.LogInfo("[DirectConnectLobby] Initialized");
        }

        public void Start()
        {
            // Nothing to do
        }

        public void Update()
        {
            // Nothing to do - no Steam packets to process
        }

        public void StartLobby()
        {
            // For direct connect, we don't create a Steam lobby
            // Instead, the server is already listening on the KCP transport
            _isInLobby = true;
            Plugin.Log.LogInfo("[DirectConnectLobby] Server started (no Steam lobby created)");
        }

        public void JoinLobby(PlatformLobby lobbyParams, bool fromUserInteraction = false)
        {
            // For direct connect, we ignore Steam lobbies
            // The connection is handled via DirectConnectManager
            Plugin.Log.LogInfo("[DirectConnectLobby] JoinLobby called - ignoring for direct connect");
        }

        public void OnShutdown()
        {
            _isInLobby = false;
            Plugin.Log.LogInfo("[DirectConnectLobby] Shutdown");
        }

        public void RequestFriendsList(FriendsMenu menu, bool manualRefresh)
        {
            // Return empty friends list for direct connect
            menu.UpdateFriendsList(new List<TechFriend>());
        }

        public bool IsInLobby()
        {
            return _isInLobby;
        }

        public PlatformLobby GetLobby()
        {
            // Return null - no Steam lobby
            return null;
        }

        public void LeaveLobby()
        {
            _isInLobby = false;
        }

        public void RejoinPreviousLobby()
        {
            // Nothing to rejoin for direct connect
        }

        public void InviteFriend(PlatformUserId friend)
        {
            // Can't invite via Steam in direct connect mode
            Plugin.Log.LogWarning("[DirectConnectLobby] Steam invites not available in direct connect mode");
        }

        public void DisplaySystemInviteScreen()
        {
            // Can't display Steam overlay in direct connect mode
            Plugin.Log.LogWarning("[DirectConnectLobby] Steam overlay not available in direct connect mode");
        }

        public void UpdateLobbyType()
        {
            // Nothing to update - no Steam lobby
        }
    }
}
