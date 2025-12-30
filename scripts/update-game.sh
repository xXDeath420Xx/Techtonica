#!/bin/bash
# Standalone script to update the game without full container restart

STEAMCMD="/opt/steamcmd/steamcmd.sh"
GAME_DIR="/opt/techtonica/game"
APP_ID="1457320"

if [ -z "$STEAM_USER" ] || [ -z "$STEAM_PASS" ]; then
    echo "ERROR: STEAM_USER and STEAM_PASS environment variables required"
    exit 1
fi

echo "Updating Techtonica..."

$STEAMCMD \
    +@sSteamCmdForcePlatformType windows \
    +force_install_dir "$GAME_DIR" \
    +login "$STEAM_USER" "$STEAM_PASS" \
    +app_update $APP_ID validate \
    +quit

echo "Update complete!"
