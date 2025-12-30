#!/bin/bash
# Build Windows server deployment package

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODS_DIR="/home/death/techtonica-server/mods"
OUTPUT_DIR="$SCRIPT_DIR/dist"
PACKAGE_NAME="TechtonicaDedicatedServer-Windows"

echo "Building Windows server package..."

# Create output directory
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR/$PACKAGE_NAME/mods"
mkdir -p "$OUTPUT_DIR/$PACKAGE_NAME/Techtonica"
mkdir -p "$OUTPUT_DIR/$PACKAGE_NAME/saves"
mkdir -p "$OUTPUT_DIR/$PACKAGE_NAME/config"

# Copy installation files
cp "$SCRIPT_DIR/install.bat" "$OUTPUT_DIR/$PACKAGE_NAME/"
cp "$SCRIPT_DIR/README.md" "$OUTPUT_DIR/$PACKAGE_NAME/"

# Copy mod DLLs
cp "$MODS_DIR/TechtonicaPreloader/bin/TechtonicaPreloader.dll" "$OUTPUT_DIR/$PACKAGE_NAME/mods/" 2>/dev/null || \
    cp "/home/death/techtonica-server/game/Techtonica/BepInEx/patchers/TechtonicaPreloader.dll" "$OUTPUT_DIR/$PACKAGE_NAME/mods/"

cp "$MODS_DIR/TechtonicaDedicatedServer/bin/Release/TechtonicaDedicatedServer.dll" "$OUTPUT_DIR/$PACKAGE_NAME/mods/" 2>/dev/null || \
    cp "/home/death/techtonica-server/game/Techtonica/BepInEx/plugins/TechtonicaDedicatedServer.dll" "$OUTPUT_DIR/$PACKAGE_NAME/mods/"

# Download BepInEx for inclusion (optional - makes package larger but more complete)
echo "Downloading BepInEx..."
wget -q --show-progress -O "$OUTPUT_DIR/$PACKAGE_NAME/BepInEx_win_x64.zip" \
    "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.2/BepInEx_win_x64_5.4.23.2.zip" || \
    echo "Warning: Could not download BepInEx (users will need to download manually)"

# Create placeholder for game files
cat > "$OUTPUT_DIR/$PACKAGE_NAME/Techtonica/COPY_GAME_FILES_HERE.txt" << 'EOF'
Copy your Techtonica game files here!

Required files from your Steam installation:
- Techtonica.exe
- Techtonica_Data\ (entire folder)
- MonoBleedingEdge\ (entire folder)
- UnityCrashHandler64.exe
- UnityPlayer.dll
- All .dll files in the root folder

You can find these files by:
1. Opening Steam
2. Right-clicking Techtonica > Properties > Local Files > Browse
3. Copying ALL files from that folder to this location

Delete this text file after copying the game files.
EOF

# Create zip
cd "$OUTPUT_DIR"
zip -r "$PACKAGE_NAME.zip" "$PACKAGE_NAME"

echo ""
echo "Package created: $OUTPUT_DIR/$PACKAGE_NAME.zip"
echo ""
ls -la "$OUTPUT_DIR/$PACKAGE_NAME.zip"
