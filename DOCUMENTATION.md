# Techtonica Dedicated Server - Complete Documentation

A community-developed solution for running dedicated Techtonica game servers without Steam dependency.

## Table of Contents

1. [Overview](#overview)
2. [How It Works](#how-it-works)
3. [Components](#components)
4. [Prerequisites](#prerequisites)
5. [Client Setup](#client-setup)
6. [Server Setup - Linux](#server-setup---linux)
7. [Server Setup - Windows](#server-setup---windows)
8. [Configuration Reference](#configuration-reference)
9. [Admin Panel](#admin-panel)
10. [Network Configuration](#network-configuration)
11. [Troubleshooting](#troubleshooting)
12. [Technical Details](#technical-details)
13. [FAQ](#faq)
14. [Contributing](#contributing)

---

## Overview

### What is this?

The Techtonica Dedicated Server project enables hosting 24/7 multiplayer servers for Techtonica that:

- **Don't require Steam** - Players connect via direct IP instead of Steam lobbies
- **Run headless** - No GPU required, perfect for VPS hosting
- **Support Linux** - Run on Linux servers using Wine
- **Auto-start** - Automatically load saves and start hosting on launch
- **Are manageable** - Web-based admin panel for configuration

### Why was this created?

Techtonica's default multiplayer uses Steam's lobby system, which:
- Requires the host to be online and playing
- Doesn't allow true dedicated server hosting
- Limits connection options to Steam friends

This project bypasses these limitations by:
- Replacing Steam networking with KCP (UDP) transport
- Patching out Steam authentication requirements
- Enabling headless operation for server hosting

### Is this against Techtonica's ToS?

This project:
- Requires a legitimate copy of Techtonica
- Does not pirate or distribute game files
- Only modifies runtime behavior (no permanent game changes)
- Is intended for legitimate multiplayer hosting

Use responsibly and at your own risk.

---

## How It Works

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    DEDICATED SERVER                          │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Techtonica.exe (via Wine on Linux)                   │   │
│  │                                                        │   │
│  │  ┌──────────────────────────────────────────────────┐ │   │
│  │  │  BepInEx Mod Framework                           │ │   │
│  │  │                                                   │ │   │
│  │  │  ┌─────────────────┐  ┌──────────────────────┐  │ │   │
│  │  │  │ TechtonicaPre   │  │ TechtonicaDedicated  │  │ │   │
│  │  │  │ loader          │  │ Server               │  │ │   │
│  │  │  │ (Steam Bypass)  │  │ (Server Logic)       │  │ │   │
│  │  │  └─────────────────┘  └──────────────────────┘  │ │   │
│  │  └──────────────────────────────────────────────────┘ │   │
│  └──────────────────────────────────────────────────────┘   │
│                          │                                   │
│                     Port 6968 (UDP)                          │
│                          │                                   │
└──────────────────────────┼───────────────────────────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
   ┌────▼────┐       ┌────▼────┐       ┌────▼────┐
   │ Client 1│       │ Client 2│       │ Client N│
   │ (Win/   │       │ (Win/   │       │ (Win/   │
   │  Linux) │       │  Linux) │       │  Linux) │
   │         │       │         │       │         │
   │ Direct  │       │ Direct  │       │ Direct  │
   │ Connect │       │ Connect │       │ Connect │
   │ Mod     │       │ Mod     │       │ Mod     │
   └─────────┘       └─────────┘       └─────────┘
```

### Mod Components

#### TechtonicaPreloader (BepInEx Patcher)

A Mono.Cecil patcher that runs BEFORE the game loads:

1. **Steam Bypass**: Replaces `SteamPlatform` constructor to skip Steam initialization
2. **Authentication Bypass**: Patches `IsClientValid()` to always return true
3. **Error Prevention**: Prevents game from quitting when Steam isn't found

This runs at the IL (bytecode) level using Mono.Cecil, modifying the game's assemblies in memory.

#### TechtonicaDedicatedServer (BepInEx Plugin)

A Harmony-based runtime plugin that:

1. **DirectConnect Manager**: Swaps FizzyFacepunch (Steam) transport with KCP (UDP)
2. **Auto-Load System**: Automatically loads saves and starts the server
3. **Console Commands**: Adds server management commands
4. **Headless Patches**: Disables unnecessary UI updates for headless operation

### Network Transport

- **Original**: FizzyFacepunch (Steam P2P relay)
- **Replaced**: KCP (reliable UDP direct connection)

KCP provides:
- Lower latency than Steam relay
- Direct IP connections
- No Steam account requirements
- Better server hosting compatibility

---

## Components

### Client Mod

**TechtonicaDirectConnect** - Install via Thunderstore/r2modman

Features:
- In-game connection UI (F8 to open)
- Direct IP:Port connection
- Remember last server
- Cross-platform (Windows/Linux)

### Server Mods

**TechtonicaPreloader** - BepInEx patcher (steam bypass)
**TechtonicaDedicatedServer** - Main server plugin

### Admin Panel (Optional)

Web-based server management:
- Server status monitoring
- Start/stop/restart controls
- Console log viewer
- Configuration editor
- Save file management
- User authentication

---

## Prerequisites

### For Clients

- Techtonica game (Steam)
- r2modman or Thunderstore Mod Manager
- OR manual BepInEx installation

### For Linux Server

| Requirement | Minimum | Recommended |
|-------------|---------|-------------|
| OS | Ubuntu 20.04+ / Debian 11+ | Ubuntu 22.04 LTS |
| CPU | 2 cores | 4 cores |
| RAM | 4 GB | 8 GB |
| Disk | 15 GB | 25 GB |
| Network | 10 Mbps | 100 Mbps |

Required packages:
- Wine 8.0+
- Xvfb (virtual framebuffer)
- unzip, wget

### For Windows Server

| Requirement | Minimum | Recommended |
|-------------|---------|-------------|
| OS | Windows 10 | Windows Server 2019+ |
| CPU | 2 cores | 4 cores |
| RAM | 4 GB | 8 GB |
| Disk | 15 GB | 25 GB |
| .NET | 4.7.2+ | Latest |

---

## Client Setup

### Method 1: r2modman (Recommended)

1. Install [r2modman](https://thunderstore.io/package/ebkr/r2modman/)
2. Select Techtonica as the game
3. Search for "TechtonicaDirectConnect"
4. Click Install
5. Launch modded game

### Method 2: Manual Installation

1. Download [BepInEx 5.4.23+](https://github.com/BepInEx/BepInEx/releases)
2. Extract to Techtonica game folder
3. Run game once to generate BepInEx folders
4. Download TechtonicaDirectConnect.dll
5. Place in `BepInEx/plugins/`
6. Launch game

### Connecting to a Server

1. Launch Techtonica
2. Press **F8** to open connection dialog
3. Enter server IP and port (e.g., `192.168.1.100:6968`)
4. Click "Connect"

---

## Server Setup - Linux

### Quick Install

```bash
# Download server package
wget https://github.com/CertiFried/TechtonicaServer/releases/latest/download/TechtonicaDedicatedServer-Linux.zip
unzip TechtonicaDedicatedServer-Linux.zip
cd TechtonicaDedicatedServer-Linux

# Run installer
./install.sh

# Copy game files (from Windows machine with Techtonica)
# to ~/techtonica-server/game/Techtonica/

# Start server
~/techtonica-server/start-server.sh
```

### Manual Install

See [deployment/linux-server/README.md](deployment/linux-server/README.md)

### Running as Service

```bash
sudo cp ~/techtonica-server/techtonica-server.service /etc/systemd/system/
sudo systemctl enable techtonica-server
sudo systemctl start techtonica-server
```

---

## Server Setup - Windows

### Quick Install

1. Download `TechtonicaDedicatedServer-Windows.zip`
2. Extract to `C:\TechtonicaServer\`
3. Copy Techtonica game files to `C:\TechtonicaServer\Techtonica\`
4. Run `install.bat`
5. Run `start-server.bat`

### Detailed Instructions

See [deployment/windows-server/README.md](deployment/windows-server/README.md)

---

## Configuration Reference

### Server Config Location

```
BepInEx/config/com.community.techtonicadedicatedserver.cfg
```

### Config Options

```ini
[Server]
# Enable direct IP connections (required)
EnableDirectConnect = true

# UDP port for game traffic
ServerPort = 6968

# Maximum concurrent players (1-8)
MaxPlayers = 8

# Auto-start server on game launch
AutoStart = true

# Path to save file for auto-load
# Leave empty to start fresh world
AutoLoadSavePath = /path/to/save.dat

[Debug]
# Enable detailed logging (for troubleshooting)
VerboseLogging = false
```

### Save File Locations

**Windows Steam:**
```
%APPDATA%\..\LocalLow\Fire Hose Games\Techtonica\Saves\
```

**Linux (Wine):**
```
~/.wine/drive_c/users/[user]/AppData/LocalLow/Fire Hose Games/Techtonica/Saves/
```

---

## Admin Panel

### Starting the Panel

```bash
cd admin-panel
npm install
npm start
```

Access: `http://your-server:6969`

### Default Credentials

- Username: `admin`
- Password: `TechtonicaAdmin2024!`

**Change this immediately after first login!**

### Features

- **Dashboard**: Server status, uptime, player count
- **Console**: View BepInEx and game logs
- **Configuration**: Edit server settings
- **Saves**: Manage save files
- **Users**: Add/remove admin accounts

---

## Network Configuration

### Required Ports

| Port | Protocol | Purpose |
|------|----------|---------|
| 6968 | UDP | Game traffic |
| 6969 | TCP | Admin panel (optional) |

### Firewall Rules

**Linux (UFW):**
```bash
sudo ufw allow 6968/udp
sudo ufw allow 6969/tcp
```

**Windows:**
```powershell
netsh advfirewall firewall add rule name="Techtonica Server" dir=in action=allow protocol=UDP localport=6968
```

### Port Forwarding (Home Networks)

If behind NAT router:
1. Access router admin (usually 192.168.1.1)
2. Find Port Forwarding / NAT
3. Add UDP port 6968 -> Server local IP

---

## Troubleshooting

### Server Issues

#### "Steam not found" or game quits immediately

**Cause**: Steam bypass not working

**Solutions**:
1. Verify `TechtonicaPreloader.dll` is in `BepInEx/patchers/`
2. Check `LogOutput.log` for "[SteamBypassPatcher]" messages
3. On Linux: Ensure `WINEDLLOVERRIDES="winhttp=n,b"` is set

#### Server starts but no one can connect

**Cause**: Network/firewall issue

**Solutions**:
1. Check server is listening: `ss -ulnp | grep 6968` (Linux) or `netstat -an | find "6968"` (Windows)
2. Verify firewall allows UDP 6968
3. Check port forwarding if behind NAT
4. Ensure clients have DirectConnect mod

#### Auto-load doesn't work

**Cause**: Save path or timing issue

**Solutions**:
1. Verify `AutoLoadSavePath` points to valid .dat file
2. Check save file isn't corrupted
3. Look for "[AutoLoad]" messages in logs

### Client Issues

#### Connection times out

**Cause**: Server unreachable

**Solutions**:
1. Verify server IP and port are correct
2. Check server is running and listening
3. Test basic connectivity: `ping server-ip`
4. Verify no firewall blocking outbound UDP

#### "Failed to initialize connection"

**Cause**: Mod conflict or corruption

**Solutions**:
1. Update DirectConnect mod
2. Check BepInEx is properly installed
3. Verify no conflicting network mods

### Linux-Specific Issues

#### Wine errors on startup

**Cause**: Wine configuration issue

**Solutions**:
1. Update Wine: `sudo apt update && sudo apt upgrade wine`
2. Reset prefix: `rm -rf ~/techtonica-server/wine/prefix && wineboot --init`
3. Check Wine version: `wine --version` (need 8.0+)

#### Xvfb display errors

**Cause**: Virtual display not running

**Solutions**:
1. Check Xvfb: `pgrep -f "Xvfb :98"`
2. Start manually: `Xvfb :98 -screen 0 1024x768x24 &`
3. Verify DISPLAY variable: `echo $DISPLAY`

---

## Technical Details

### BepInEx Loading Order

1. **doorstop** hooks into Unity's entry point
2. **Preloader** loads patchers (TechtonicaPreloader)
3. **Chainloader** loads plugins (TechtonicaDedicatedServer)

### Steam Bypass Mechanism

The preloader modifies `SteamPlatform` class in Assembly-CSharp:

```csharp
// Original constructor calls Steam APIs
// Patched constructor is empty - skips all Steam initialization

// Original IsClientValid() checks Steam connection
// Patched IsClientValid() returns true always
```

### Network Transport Swap

```csharp
// Original: FizzyFacepunch (Steam P2P)
NetworkManager.singleton.transport = originalFizzyTransport;

// After EnableDirectConnect():
NetworkManager.singleton.transport = kcpTransport;
```

### Unity Main Thread Handling

Since Wine doesn't reliably call Unity callbacks, we use:

```csharp
// Capture during Awake()
_unitySyncContext = SynchronizationContext.Current;

// Post from background thread
_unitySyncContext.Post(_ => {
    // Runs on main thread
    AutoLoadManager.TryAutoLoad();
}, null);
```

---

## FAQ

### Can I host multiple servers on one machine?

Yes! Each server needs:
- Different `ServerPort` in config
- Separate game installation folder
- Separate process

### Does this work with other mods?

Generally yes, as long as they don't conflict with:
- Network/transport modifications
- Steam authentication
- The save system

### Will game updates break this?

Possibly. Major updates may require mod updates. Always:
- Keep mod backups
- Wait for mod updates after game patches
- Check GitHub/Discord for compatibility info

### Can players use Steam multiplayer while this is installed?

The DirectConnect mod only activates when connecting via IP. Normal Steam multiplayer should still work, but disable the mod if issues occur.

### Is there a player limit?

Default maximum is 8 players, matching Techtonica's designed limit. Higher may cause issues.

### How do I backup server data?

Backup these folders:
- `saves/` - World saves
- `BepInEx/config/` - Server configuration
- `admin-panel/users.json` - Admin accounts

---

## Contributing

### Reporting Issues

1. Check existing issues first
2. Include:
   - OS and version
   - Mod versions
   - Log files (LogOutput.log, server.log)
   - Steps to reproduce

### Development Setup

```bash
git clone https://github.com/CertiFried/TechtonicaServer
cd TechtonicaServer

# Build mods
cd mods/TechtonicaPreloader && dotnet build
cd ../TechtonicaDedicatedServer && dotnet build

# Build client mod
cd ../TechtonicaDirectConnect && dotnet build
```

### Pull Requests

1. Fork the repository
2. Create feature branch
3. Test thoroughly
4. Submit PR with description

---

## Credits

- **CertiFried Community** - Development and hosting
- **BepInEx Team** - Mod framework
- **Mirror Networking** - Network transport foundation
- **Techtonica Community** - Testing and feedback

## License

MIT License - See [LICENSE](LICENSE)

---

*Documentation version: 1.0.0*
*Last updated: December 2024*
