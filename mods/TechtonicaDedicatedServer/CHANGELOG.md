# Changelog

All notable changes to this project will be documented in this file.

## [0.1.0] - Initial Release

### Added
- Direct IP connection support via KCP transport
- Console commands for server management (`ds.host`, `ds.connect`, etc.)
- Configuration file for server settings
- Harmony patches for network transport switching
- Steam lobby bypass for direct connections
- Player tracking and kick functionality
- Headless server mode support
- Docker deployment support (experimental)

### Known Issues
- Headless mode requires additional testing
- Save file sync between host and clients needs verification
- Some edge cases in connection handling

### Planned Features
- Web-based server management panel
- Discord bot integration
- Automatic save backups
- Password protection
- Whitelist/blacklist support
