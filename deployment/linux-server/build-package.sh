#!/bin/bash
# Build Linux server deployment package

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODS_DIR="/home/death/techtonica-server/mods"
OUTPUT_DIR="$SCRIPT_DIR/dist"
PACKAGE_NAME="TechtonicaDedicatedServer-Linux"

echo "Building Linux server package..."

# Create output directory
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR/$PACKAGE_NAME/mods"

# Copy installation files
cp "$SCRIPT_DIR/install.sh" "$OUTPUT_DIR/$PACKAGE_NAME/"
cp "$SCRIPT_DIR/README.md" "$OUTPUT_DIR/$PACKAGE_NAME/"

# Copy mod DLLs
cp "$MODS_DIR/TechtonicaPreloader/bin/TechtonicaPreloader.dll" "$OUTPUT_DIR/$PACKAGE_NAME/mods/" 2>/dev/null || \
    cp "/home/death/techtonica-server/game/Techtonica/BepInEx/patchers/TechtonicaPreloader.dll" "$OUTPUT_DIR/$PACKAGE_NAME/mods/"

cp "$MODS_DIR/TechtonicaDedicatedServer/bin/Release/TechtonicaDedicatedServer.dll" "$OUTPUT_DIR/$PACKAGE_NAME/mods/" 2>/dev/null || \
    cp "/home/death/techtonica-server/game/Techtonica/BepInEx/plugins/TechtonicaDedicatedServer.dll" "$OUTPUT_DIR/$PACKAGE_NAME/mods/"

# Create zip
cd "$OUTPUT_DIR"
zip -r "$PACKAGE_NAME.zip" "$PACKAGE_NAME"

echo ""
echo "Package created: $OUTPUT_DIR/$PACKAGE_NAME.zip"
echo ""
ls -la "$OUTPUT_DIR/$PACKAGE_NAME.zip"
