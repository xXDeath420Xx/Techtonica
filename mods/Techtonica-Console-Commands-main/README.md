# Techtonica Console Commands

![Banner Image](https://imgur.com/a/K7rpPhw)

## Description

This mod allows for a simple way to manage your game. Give yourself unlimited items, unlock tech for free, and more!

## Screenshots
I can't be asked to make screenshots for a pre-release :)

&nbsp;
## Installation

If you are using Techtonica Mod Loader, you can ignore this section.

Note, this mod requires use of the BepInEx Update function. If you have not already done so for another mod, follow these instructions:
1. Find your game install folder.
2. Navigate to BepInEx\config.
3. Open BepInEx.cfg.
4. Find the setting "HideGameManagerObject".
5. Set it to "true".
6. For this mod specifically, find the setting "Enabled" under the tag "Logging.Console".
7. Set it to "true". This will let you see command output.
8. Save & close.

### Techtonica Mod Loader Installation

You can download the Techtonica Mod Loader from [here](https://github.com/CubeSuite/TechtonicaModLoader/releases) and use that to install this mod.

### Manual Install Instructions

Note: If you are playing on Gamepass, your game version is likely behind the steam version. Please check the version compatibility chart below.

Your game folder is likely in one of these places:  
    • Steam: (A-Z):/steam/steamapps/common/Techtonica  
    • Gamepass: (A-Z):/XboxGames/Techtonica/Content  
    • Gamepass: Could also be in C:/Program Data/WindowsApps  

1. Download BepInEx v5.4.21 from [here](https://github.com/BepInEx/BepInEx/releases)
2. Follow the installation instructions [here](https://docs.bepinex.dev/articles/user_guide/installation/index.html)
3. Extract the contents of the .zip file for this mod.
4. Drag the "BepInEx" folder into your game folder.

## Version Compatibility

| Mod Version | Game Version |
|-------------|--------------|
| v0.1.0      | v0.3.0e      |
| v0.2.0      | v0.3.0e      |

## Changelog

### V0.1.0

Pre-release 1.

### V0.2.0

Pre-release 2.

Changes:
- Fixed a nullreference exception that occured in rare cases when pressing enter while there was nothing in the console.
- Minor changes to grammar and wording in command outputs.
- Added setwarp
- Added delwarp
- Added warps.txt
- The console should now automatically be focused.
- Your command history is now displayed above the console.
- You can now press page up/down to cycle through previously inputted commands.
- Messed around with how noclip works, didn't achieve anything. If you notice anything different please let me know.
- Inputting incorrect commands now no longer breaks the history system. (Which you couldn't notice before, anyway.)

&nbsp;
## Usage
### Commands Reference:

! Open Console: / key !

    give <itemname> <amount>
give (local!) player items. Item name is the display name of the item without spaces.

    setplayerparams <parameter> <value>
set various variables inside the playercontroller. For the parameter choose from choose from: run, walk, fly, jump, scan (speed).

    echo <stringtolog> <logtype>
log a string in the console. For the logtype choose from: info, warning, error, fatal, or message (determines color)

    tp <X> <Y> <Z>
X, Y, and Z coordinates, each can also be replaced with ~ to use current player position (of that component).

    warp <location>
Warp to a specific location, for example "lima", "victor", "freight", "xray", and "waterfall" (waterfall facility), or teleport to a warp previously set.

    setwarp <name>
Set a warp at current player location with a name.

    delwarp <name>
Delete a warp with name.

    unlock <nodename> <bShouldDrawPower>
Unlock a techtree upgrade. Nodename is the name of the techtree node without spaces. For bshoulddrawpower false is recommended. Tip: Use "unlock all false" to quickly unlock everything! (expect A LOT of dialogue spam)

    opensesame
opens whatever door you're currently looking at/near. (in a radius of 8 voxels/powerfloors)

    weightless
toggles weightlessness

    instamole
toggles instamining

    echolocation (DEBUG)
logs your current location in the console. You can use this information to make warps. (Soon to be implemented!)

    cammode <MODE>
changes your camera mode. keep in mind for freecam using noclip is usually more convenient.

    noclip
toggles noclip.

    gamespeed <amount>
Set a multiplier for the game's speed.

    bind <key> <command>{arg1,arg2}
Bind a key to a command. Keys are case sensitive. Use {arg1,arg2} for arguments in the command (no spaces inside of the brackets {}).

    setsize <size> <scaleparams>
Set the player size. Scaleparams for scaling the player parameters such as walk speed.

    clear <item> <amount>
Clear your inventory of a certain amount of an item.

    setmoledimensions <x> <y> <z>
Set the 3D size of your mole radius. Values greater than or equal to 25 are not as performant.

## Disclaimer

Note: NEW Games must be loaded, saved, and reloaded for mods to take effect. Existing saves will auto-apply mods. 
Please be sure to backup your saves before using mods: AppData\LocalLow\Fire Hose Games\Techtonica 
USE AT YOUR OWN RISK! Techtonica Devs do not provide support for Mods, and cannot recover saves damaged by mod usage.

Some assets may come from Techtonica or from the website created and owned by Fire Hose Games, who hold the copyright of Techtonica. All trademarks and registered trademarks present in any images are proprietary to Fire Hose Games.