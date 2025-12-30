# Changelog

## 1.0.17
- Added patches for NetworkedPlayer.OnStartClient() and OnStartLocalPlayer()
- These methods crash when connecting because the player spawns before scene objects exist
- Now patches 5 methods total to prevent NullReferenceException spam

## 1.0.16
- Added null safety patches to prevent error spam when connecting
- Patches NetworkedPlayer.Update() to skip (prevents NullReferenceException)
- Patches ThirdPersonDisplayAnimator.Update() to skip (prevents NullReferenceException)
- Patches ThirdPersonDisplayAnimator.UpdateSillyStuff() to skip (prevents NullReferenceException)
- These errors occurred because the game creates player objects before they're fully initialized

## 1.0.15
- Added NetworkClient.Ready() call after connection
- Added NetworkClient.AddPlayer() to request player spawning
- Increased connection timeout to 15 seconds
- Added detailed logging for connection/ready/spawn sequence

## 1.0.14
- Fixed double-toggle bug (OnGUI is called multiple times per frame)
- Added frame guard to ensure UI only toggles once per key press
- Event.current now properly detected and working!

## 1.0.13
- Complete rewrite following ConsoleCommands mod pattern
- Update/OnGUI now run directly on plugin (like working mods)
- Added [BepInProcess("Techtonica.exe")] attribute
- Added HideFlags.HideAndDontSave to persist gameObject
- Triple input detection: Unity Input, Windows API, and Event.current
- Removed separate MonoBehaviour (was not initializing properly)

## 1.0.12
- Added robust logging for DirectConnectBehaviour lifecycle (Awake, Start)
- Added fallback F11 detection via Unity's Event.current in OnGUI
- Added error handling to prevent silent failures
- Fixed potential null reference issues in Update/OnGUI

## 1.0.11
- Updated changelog with all version history

## 1.0.10
- Updated mod icon

## 1.0.9
- Fixed Update/OnGUI not being called by creating dedicated MonoBehaviour
- Changed keybind from F8 to F11 (F1-F10 are used in-game)

## 1.0.8
- Added debug logging to diagnose input detection issues

## 1.0.7
- Added CHANGELOG.md to package

## 1.0.6
- Attempted changelog fix

## 1.0.5
- Fixed input detection for games using Rewired input system
- Now uses Windows API (GetAsyncKeyState) to detect key presses
- Works regardless of game's input system

## 1.0.4
- Added alternative input detection via OnGUI events
- Added debug logging for UI toggle

## 1.0.3
- Version bump to force Thunderstore cache refresh

## 1.0.2
- Added missing kcp2k.dll dependency (fixes mod not loading)

## 1.0.1
- Updated README with server hosting information
- Fixed GitHub repository links

## 1.0.0
- Initial release
- Direct IP connection to Techtonica dedicated servers
- In-game UI with F8 hotkey
- KCP transport for reliable UDP connections
- Auto-saves last connected server
