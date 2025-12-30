# Techtonica Direct Connect

Connect to Techtonica dedicated servers using direct IP addresses instead of Steam lobbies!

## Features

- **Direct IP Connection**: Connect to any Techtonica server using IP:Port
- **In-Game UI**: Simple connect dialog accessible via hotkey
- **Cross-Platform**: Works on both Windows and Linux
- **Remember Last Server**: Automatically saves your last connected server
- **Easy Disconnect**: One-click disconnect from the in-game menu

## Installation

### Using r2modman / Thunderstore Mod Manager (Recommended)
1. Install [r2modman](https://thunderstore.io/package/ebkr/r2modman/) or Thunderstore Mod Manager
2. Search for "Techtonica Direct Connect"
3. Click Install

### Manual Installation
1. Install [BepInEx 5.4.21+](https://github.com/BepInEx/BepInEx/releases) to your Techtonica game folder
2. Download and extract `TechtonicaDirectConnect.dll` to `BepInEx/plugins/`
3. Launch the game

## Usage

1. Launch Techtonica
2. Press **F8** (configurable) to open the connect dialog
3. Enter the server IP address and port
4. Click "Connect"
5. To disconnect, press F8 again and click "Disconnect"

## Configuration

After first run, a config file is created at:
`BepInEx/config/com.certifried.techtonicadirectconnect.cfg`

| Setting | Default | Description |
|---------|---------|-------------|
| DefaultPort | 6968 | Default server port |
| LastServerAddress | (empty) | Last connected server (auto-saved) |
| ConnectHotkey | F8 | Hotkey to open connect dialog |

## Finding Servers

Check these resources for community servers:
- [Techtonica Discord](https://discord.gg/techtonica) - Server listings channel
- [CertiFried Community](https://certifriedmultitool.com) - Official servers

## Server Hosting

Want to host your own server? Check out the companion mod:
- **TechtonicaDedicatedServer** - Full dedicated server hosting solution

## Troubleshooting

### "Connection timed out"
- Verify the server IP and port are correct
- Check if the server is running and accepting connections
- Ensure your firewall allows outbound UDP on the server port

### "Failed to initialize connection"
- Make sure BepInEx is installed correctly
- Check `BepInEx/LogOutput.log` for error details

### Game freezes on connect
- This can happen if the server is unreachable
- Wait for the timeout (10 seconds) or restart the game

## Technical Details

- Uses KCP (UDP) transport for reliable connections
- Swaps the default Steam transport at runtime
- Compatible with vanilla game saves
- Does not modify game files

## Credits

- Developed by the CertiFried Community
- Based on Mirror networking research
- Thanks to the Techtonica modding community

## Support

- Report issues on [GitHub](https://github.com/CertiFried/TechtonicaDirectConnect/issues)
- Join the [CertiFried Discord](https://discord.gg/certifried)

## License

MIT License - See LICENSE file for details
