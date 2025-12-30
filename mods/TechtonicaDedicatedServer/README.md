# Techtonica Dedicated Server

**The most requested mod for Techtonica!** This mod enables dedicated server hosting and direct IP connections, allowing you to run 24/7 servers without requiring Steam lobbies.

## Features

- **Direct IP Connections** - Connect to servers via IP address instead of Steam invites
- **Dedicated Server Mode** - Run headless servers without a local player
- **Host Mode** - Run as both server and player (like vanilla, but with direct IP)
- **Console Commands** - Full server management via in-game console
- **No Second Steam Account Required** - Players connect directly via IP

## Installation

1. Install [BepInEx](https://thunderstore.io/c/techtonica/p/BepInEx/BepInExPack/) if you haven't already
2. Download this mod and extract to your `BepInEx/plugins` folder
3. Launch the game

## Usage

### Starting a Server

Open the in-game console (usually `~` or `F1`) and use these commands:

```
ds.host [port]       - Start as host (you play + others can join)
ds.server [port]     - Start dedicated server (no local player)
```

Example:
```
ds.host 6968
```

### Connecting to a Server

```
ds.connect <ip:port> - Connect to a server
```

Example:
```
ds.connect 192.168.1.100:6968
ds.connect myserver.example.com:6968
```

### Server Management

```
ds.status            - Show server status
ds.players           - List connected players
ds.kick <id>         - Kick a player by connection ID
ds.say <message>     - Broadcast a message
ds.stop              - Stop server or disconnect
```

## Configuration

Edit `BepInEx/config/com.community.techtonicadedicatedserver.cfg`:

```ini
[General]
# Enable direct IP connections (bypasses Steam lobbies)
EnableDirectConnect = true

[Server]
# Port for the dedicated server
Port = 6968

# Maximum number of players
MaxPlayers = 8

# Server password (empty = no password)
Password =

# Run in headless mode (no graphics)
HeadlessMode = false

# Auto-start server on game launch
AutoStartServer = false
```

## Running a Headless Server

For true dedicated server hosting (no GUI):

1. Set `HeadlessMode = true` and `AutoStartServer = true` in config
2. Launch with: `Techtonica.exe -batchmode -nographics`
3. Server will auto-start on the configured port

### Docker Support

This mod is designed to work with Docker for containerized deployments. See our [Docker setup guide](https://github.com/yourusername/TechtonicaDedicatedServer/wiki/Docker-Setup) for details.

## Port Forwarding

For internet play, forward these ports on your router:
- **6968 UDP** - Game traffic (or your configured port)
- **6968 TCP** - Game traffic (or your configured port)

## Compatibility

- **Game Version**: Tested on 2024.11.04 and later
- **BepInEx Version**: 5.4.21+
- **Multiplayer**: All connected players need this mod installed

## Known Limitations

- Save files are stored on the host/server
- All players must have the mod installed
- Steam achievements may not work in direct connect mode
- Some Steam-specific features (invites, overlay) won't work

## Troubleshooting

### "Connection timed out"
- Check your firewall allows the port
- Verify port forwarding if connecting over internet
- Ensure both players have the same mod version

### "NetworkManager not found"
- Make sure you're in-game (not main menu)
- Try loading a save first, then starting server

### Server crashes on start
- Check BepInEx logs at `BepInEx/LogOutput.log`
- Verify no conflicting mods

## Contributing

Found a bug or want to contribute? Visit our [GitHub repository](https://github.com/yourusername/TechtonicaDedicatedServer).

## Credits

- **Fire Hose Games** - For making Techtonica
- **BepInEx Team** - For the modding framework
- **Mirror Networking** - For the networking library
- **The Techtonica Modding Community** - For research and support

## License

MIT License - See LICENSE file for details.

---

*This mod is not affiliated with Fire Hose Games. Use at your own risk.*
