#!/bin/bash
#############################################
# Techtonica Dedicated Server - Linux Setup
# One-click installation script
# CertiFried Community - https://certifriedmultitool.com
#############################################

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
INSTALL_DIR="${INSTALL_DIR:-$HOME/techtonica-server}"
GAME_DIR="$INSTALL_DIR/game/Techtonica"
WINE_PREFIX="$INSTALL_DIR/wine/prefix"
SAVES_DIR="$INSTALL_DIR/saves"
CONFIG_DIR="$INSTALL_DIR/config"

# BepInEx download URL (stable release)
BEPINEX_URL="https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.2/BepInEx_win_x64_5.4.23.2.zip"

echo -e "${BLUE}"
echo "╔═══════════════════════════════════════════════════════════╗"
echo "║     Techtonica Dedicated Server - Linux Installation      ║"
echo "║          CertiFried Community Server Solution             ║"
echo "╚═══════════════════════════════════════════════════════════╝"
echo -e "${NC}"

# Function to print status
print_status() {
    echo -e "${GREEN}[✓]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[!]${NC} $1"
}

print_error() {
    echo -e "${RED}[✗]${NC} $1"
}

print_info() {
    echo -e "${BLUE}[i]${NC} $1"
}

# Check if running as root
if [ "$EUID" -eq 0 ]; then
    print_error "Please do not run this script as root"
    print_info "Run as a regular user: ./install.sh"
    exit 1
fi

# Check prerequisites
echo ""
echo -e "${YELLOW}Checking prerequisites...${NC}"
echo ""

MISSING_DEPS=""

# Check Wine
if ! command -v wine &> /dev/null; then
    MISSING_DEPS="$MISSING_DEPS wine"
fi

# Check Xvfb
if ! command -v Xvfb &> /dev/null; then
    MISSING_DEPS="$MISSING_DEPS xvfb"
fi

# Check unzip
if ! command -v unzip &> /dev/null; then
    MISSING_DEPS="$MISSING_DEPS unzip"
fi

# Check wget
if ! command -v wget &> /dev/null; then
    MISSING_DEPS="$MISSING_DEPS wget"
fi

if [ -n "$MISSING_DEPS" ]; then
    print_error "Missing required packages:$MISSING_DEPS"
    echo ""
    echo "Install them using your package manager:"
    echo ""
    echo "  Ubuntu/Debian:"
    echo "    sudo apt update"
    echo "    sudo apt install wine wine64 xvfb unzip wget"
    echo ""
    echo "  Fedora:"
    echo "    sudo dnf install wine xorg-x11-server-Xvfb unzip wget"
    echo ""
    echo "  Arch Linux:"
    echo "    sudo pacman -S wine xorg-server-xvfb unzip wget"
    echo ""
    exit 1
fi

print_status "Wine installed: $(wine --version)"
print_status "Xvfb installed"
print_status "unzip installed"
print_status "wget installed"

# Create directories
echo ""
echo -e "${YELLOW}Creating directories...${NC}"

mkdir -p "$INSTALL_DIR"
mkdir -p "$GAME_DIR"
mkdir -p "$WINE_PREFIX"
mkdir -p "$SAVES_DIR"
mkdir -p "$CONFIG_DIR"
mkdir -p "$GAME_DIR/BepInEx/plugins"
mkdir -p "$GAME_DIR/BepInEx/patchers"
mkdir -p "$GAME_DIR/BepInEx/config"

print_status "Created $INSTALL_DIR"

# Initialize Wine prefix
echo ""
echo -e "${YELLOW}Initializing Wine prefix...${NC}"

export WINEPREFIX="$WINE_PREFIX"
export WINEARCH=win64
wineboot --init 2>/dev/null || true
print_status "Wine prefix initialized at $WINE_PREFIX"

# Check if game files exist
echo ""
echo -e "${YELLOW}Checking game files...${NC}"

if [ ! -f "$GAME_DIR/Techtonica.exe" ]; then
    print_warning "Game files not found!"
    echo ""
    echo "Please copy your Techtonica game files to:"
    echo "  $GAME_DIR"
    echo ""
    echo "Required files:"
    echo "  - Techtonica.exe"
    echo "  - Techtonica_Data/ folder"
    echo "  - MonoBleedingEdge/ folder"
    echo "  - UnityCrashHandler64.exe"
    echo ""
    echo "You can find these in your Steam library:"
    echo "  Right-click Techtonica > Properties > Local Files > Browse"
    echo ""
    NEED_GAME_FILES=true
else
    print_status "Game files found"
    NEED_GAME_FILES=false
fi

# Download and install BepInEx
echo ""
echo -e "${YELLOW}Installing BepInEx...${NC}"

BEPINEX_ZIP="/tmp/BepInEx.zip"
if [ ! -f "$GAME_DIR/BepInEx/core/BepInEx.dll" ]; then
    wget -q --show-progress -O "$BEPINEX_ZIP" "$BEPINEX_URL"
    unzip -o "$BEPINEX_ZIP" -d "$GAME_DIR"
    rm -f "$BEPINEX_ZIP"
    print_status "BepInEx installed"
else
    print_status "BepInEx already installed"
fi

# Install mods
echo ""
echo -e "${YELLOW}Installing server mods...${NC}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Copy preloader
if [ -f "$SCRIPT_DIR/mods/TechtonicaPreloader.dll" ]; then
    cp "$SCRIPT_DIR/mods/TechtonicaPreloader.dll" "$GAME_DIR/BepInEx/patchers/"
    print_status "Installed TechtonicaPreloader"
else
    print_warning "TechtonicaPreloader.dll not found in mods/ folder"
fi

# Copy dedicated server plugin
if [ -f "$SCRIPT_DIR/mods/TechtonicaDedicatedServer.dll" ]; then
    cp "$SCRIPT_DIR/mods/TechtonicaDedicatedServer.dll" "$GAME_DIR/BepInEx/plugins/"
    print_status "Installed TechtonicaDedicatedServer"
else
    print_warning "TechtonicaDedicatedServer.dll not found in mods/ folder"
fi

# Create default config
echo ""
echo -e "${YELLOW}Creating default configuration...${NC}"

CONFIG_FILE="$GAME_DIR/BepInEx/config/com.community.techtonicadedicatedserver.cfg"
if [ ! -f "$CONFIG_FILE" ]; then
    cat > "$CONFIG_FILE" << 'EOFCONFIG'
## Settings file was created by plugin Techtonica Dedicated Server v0.1.0
## Plugin GUID: com.community.techtonicadedicatedserver

[Server]

## Enable direct IP connections (bypasses Steam)
EnableDirectConnect = true

## Server port for direct connections
ServerPort = 6968

## Maximum number of players
MaxPlayers = 8

## Enable auto-start on launch
AutoStart = true

## Save file path for auto-load (leave empty to start fresh)
AutoLoadSavePath =

[Debug]

## Enable verbose logging
VerboseLogging = false
EOFCONFIG
    print_status "Created default config"
else
    print_status "Config already exists"
fi

# Create startup script
echo ""
echo -e "${YELLOW}Creating startup scripts...${NC}"

cat > "$INSTALL_DIR/start-server.sh" << 'EOFSTART'
#!/bin/bash
# Techtonica Dedicated Server Startup Script

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GAME_DIR="$SCRIPT_DIR/game/Techtonica"
WINE_PREFIX="$SCRIPT_DIR/wine/prefix"

# Check if Xvfb is running on display :98
if ! pgrep -f "Xvfb :98" > /dev/null; then
    echo "Starting Xvfb on display :98..."
    Xvfb :98 -screen 0 1024x768x24 &
    sleep 2
fi

export DISPLAY=:98
export WINEPREFIX="$WINE_PREFIX"
export WINEDLLOVERRIDES="winhttp=n,b"

cd "$GAME_DIR"

echo "Starting Techtonica dedicated server..."
echo "Press Ctrl+C to stop"
echo ""

wine Techtonica.exe -batchmode -logfile "$SCRIPT_DIR/game.log" 2>&1 | while read line; do
    echo "[$(date '+%H:%M:%S')] $line"
done

echo "Server stopped"
EOFSTART

chmod +x "$INSTALL_DIR/start-server.sh"
print_status "Created start-server.sh"

# Create stop script
cat > "$INSTALL_DIR/stop-server.sh" << 'EOFSTOP'
#!/bin/bash
# Stop Techtonica Dedicated Server

echo "Stopping Techtonica server..."

# Find and kill Wine/Techtonica process
pkill -f "Techtonica.exe" 2>/dev/null && echo "Server stopped" || echo "Server was not running"
EOFSTOP

chmod +x "$INSTALL_DIR/stop-server.sh"
print_status "Created stop-server.sh"

# Create systemd service file
cat > "$INSTALL_DIR/techtonica-server.service" << EOFSERVICE
[Unit]
Description=Techtonica Dedicated Server
After=network.target

[Service]
Type=simple
User=$USER
WorkingDirectory=$INSTALL_DIR
ExecStart=$INSTALL_DIR/start-server.sh
ExecStop=$INSTALL_DIR/stop-server.sh
Restart=on-failure
RestartSec=10

Environment="DISPLAY=:98"
Environment="WINEPREFIX=$WINE_PREFIX"
Environment="WINEDLLOVERRIDES=winhttp=n,b"

[Install]
WantedBy=multi-user.target
EOFSERVICE

print_status "Created systemd service file"

# Summary
echo ""
echo -e "${GREEN}╔═══════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║              Installation Complete!                        ║${NC}"
echo -e "${GREEN}╚═══════════════════════════════════════════════════════════╝${NC}"
echo ""
echo "Installation directory: $INSTALL_DIR"
echo ""

if [ "$NEED_GAME_FILES" = true ]; then
    echo -e "${YELLOW}NEXT STEPS:${NC}"
    echo ""
    echo "1. Copy your Techtonica game files to:"
    echo "   $GAME_DIR"
    echo ""
    echo "2. Copy a save file to load (optional):"
    echo "   $SAVES_DIR/"
    echo ""
    echo "3. Edit the config file:"
    echo "   $CONFIG_FILE"
    echo ""
    echo "4. Start the server:"
    echo "   $INSTALL_DIR/start-server.sh"
else
    echo "To start the server:"
    echo "  $INSTALL_DIR/start-server.sh"
    echo ""
    echo "To stop the server:"
    echo "  $INSTALL_DIR/stop-server.sh"
fi

echo ""
echo "For systemd service (auto-start on boot):"
echo "  sudo cp $INSTALL_DIR/techtonica-server.service /etc/systemd/system/"
echo "  sudo systemctl enable techtonica-server"
echo "  sudo systemctl start techtonica-server"
echo ""
echo "Server will listen on port 6968 (UDP)"
echo "Make sure to open this port in your firewall!"
echo ""
echo -e "${BLUE}Documentation: https://github.com/xXDeath420Xx/Techtonica${NC}"
echo -e "${BLUE}Support: https://discord.com/invite/mJfbDgWA7z${NC}"
echo ""
