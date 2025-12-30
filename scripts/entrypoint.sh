#!/bin/bash
set -e

echo "=========================================="
echo "  Techtonica Dedicated Server Launcher"
echo "=========================================="

# Paths
STEAMCMD="/opt/steamcmd/steamcmd.sh"
GAME_DIR="/opt/techtonica/game"
BEPINEX_DIR="/opt/techtonica/bepinex"
SAVES_DIR="/opt/techtonica/saves"
LOGS_DIR="/opt/techtonica/logs"

# Techtonica App ID
APP_ID="1457320"

# ============================================
# Start Xvfb (Virtual Display)
# ============================================
start_xvfb() {
    echo "[*] Starting Xvfb virtual display on :99..."
    Xvfb :99 -screen 0 1024x768x24 &
    XVFB_PID=$!
    sleep 2

    if kill -0 $XVFB_PID 2>/dev/null; then
        echo "[+] Xvfb started successfully (PID: $XVFB_PID)"
        export DISPLAY=:99
    else
        echo "[!] Failed to start Xvfb"
        exit 1
    fi
}

# ============================================
# Initialize Wine Prefix
# ============================================
init_wine() {
    echo "[*] Initializing Wine prefix..."

    if [ ! -d "$WINEPREFIX/drive_c" ]; then
        echo "[*] Creating new Wine prefix (this may take a minute)..."
        wineboot --init

        # Install common dependencies
        echo "[*] Installing Wine dependencies..."
        winetricks -q vcrun2019 dotnet48 || true
    else
        echo "[+] Wine prefix already exists"
    fi
}

# ============================================
# Update/Install Game via SteamCMD
# ============================================
update_game() {
    if [ "$SKIP_UPDATE" = "1" ] && [ -f "$GAME_DIR/Techtonica.exe" ]; then
        echo "[*] Skipping game update (SKIP_UPDATE=1)"
        return 0
    fi

    echo "[*] Updating game via SteamCMD..."

    if [ -z "$STEAM_USER" ] || [ -z "$STEAM_PASS" ]; then
        echo "[!] ERROR: STEAM_USER and STEAM_PASS must be set"
        echo "[!] Techtonica requires an authenticated Steam account that owns the game"
        exit 1
    fi

    # Run SteamCMD
    # Note: First run requires Steam Guard code - see docs
    $STEAMCMD \
        +@sSteamCmdForcePlatformType windows \
        +force_install_dir "$GAME_DIR" \
        +login "$STEAM_USER" "$STEAM_PASS" \
        +app_update $APP_ID validate \
        +quit

    if [ $? -eq 0 ]; then
        echo "[+] Game updated successfully"
    else
        echo "[!] SteamCMD failed - may need Steam Guard authentication"
        echo "[!] Run container interactively first: docker run -it --entrypoint bash <image>"
        exit 1
    fi
}

# ============================================
# Install/Update BepInEx
# ============================================
install_bepinex() {
    echo "[*] Checking BepInEx installation..."

    BEPINEX_TARGET="$GAME_DIR/BepInEx"

    if [ ! -d "$BEPINEX_TARGET/core" ]; then
        echo "[*] Installing BepInEx..."

        if [ -f "$BEPINEX_DIR/BepInEx.zip" ]; then
            unzip -o "$BEPINEX_DIR/BepInEx.zip" -d "$GAME_DIR/"
            echo "[+] BepInEx installed from local archive"
        else
            echo "[!] BepInEx not found at $BEPINEX_DIR/BepInEx.zip"
            echo "[!] Download from: https://github.com/BepInEx/BepInEx/releases"
            echo "[!] Use: BepInEx_x64_5.4.21.0.zip (or latest 5.x for Unity)"
        fi
    else
        echo "[+] BepInEx already installed"
    fi

    # Link custom mods
    if [ -d "/opt/techtonica/mods" ]; then
        echo "[*] Linking custom mods..."
        mkdir -p "$BEPINEX_TARGET/plugins"
        for mod in /opt/techtonica/mods/*.dll; do
            if [ -f "$mod" ]; then
                ln -sf "$mod" "$BEPINEX_TARGET/plugins/" 2>/dev/null || true
            fi
        done
    fi
}

# ============================================
# Link Save Files
# ============================================
setup_saves() {
    echo "[*] Setting up save file links..."

    # Techtonica saves to: %USERPROFILE%/AppData/LocalLow/Fire Hose Games/Techtonica/
    WINE_SAVES="$WINEPREFIX/drive_c/users/steam/AppData/LocalLow/Fire Hose Games/Techtonica"

    mkdir -p "$WINE_SAVES"

    # If we have saves to restore, copy them
    if [ -d "$SAVES_DIR" ] && [ "$(ls -A $SAVES_DIR 2>/dev/null)" ]; then
        echo "[*] Restoring saves from mounted volume..."
        cp -r "$SAVES_DIR"/* "$WINE_SAVES/" 2>/dev/null || true
    fi
}

# ============================================
# Start Game
# ============================================
start_game() {
    echo "[*] Starting Techtonica..."

    cd "$GAME_DIR"

    # Log file
    LOG_FILE="$LOGS_DIR/techtonica_$(date +%Y%m%d_%H%M%S).log"

    # Launch with Wine
    # Note: May need adjustments based on how the game handles headless
    echo "[*] Launching via Wine (logging to $LOG_FILE)..."

    wine64 Techtonica.exe 2>&1 | tee "$LOG_FILE" &
    GAME_PID=$!

    echo "[+] Game started (PID: $GAME_PID)"
    echo "[*] Waiting for game to initialize..."

    # Wait for game process
    wait $GAME_PID
}

# ============================================
# Graceful Shutdown
# ============================================
cleanup() {
    echo ""
    echo "[*] Shutting down..."

    # Try graceful shutdown of Wine
    wineserver -k 2>/dev/null || true

    # Backup saves
    if [ -d "$WINEPREFIX/drive_c/users/steam/AppData/LocalLow/Fire Hose Games/Techtonica" ]; then
        echo "[*] Backing up saves..."
        cp -r "$WINEPREFIX/drive_c/users/steam/AppData/LocalLow/Fire Hose Games/Techtonica"/* "$SAVES_DIR/" 2>/dev/null || true
    fi

    # Kill Xvfb
    kill $XVFB_PID 2>/dev/null || true

    echo "[+] Shutdown complete"
    exit 0
}

trap cleanup SIGTERM SIGINT

# ============================================
# Main
# ============================================
main() {
    start_xvfb
    init_wine
    update_game
    install_bepinex
    setup_saves
    start_game
}

main
