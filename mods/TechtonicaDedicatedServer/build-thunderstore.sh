#!/bin/bash
# Build script for Thunderstore package

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

VERSION="0.1.0"
PACKAGE_NAME="TechtonicaDedicatedServer"
OUTPUT_DIR="./dist"
THUNDERSTORE_DIR="$OUTPUT_DIR/thunderstore"

echo "Building $PACKAGE_NAME v$VERSION for Thunderstore..."

# Create output directories
mkdir -p "$THUNDERSTORE_DIR"

# Check if DLL exists (built externally)
if [ ! -f "bin/Release/TechtonicaDedicatedServer.dll" ]; then
    echo "ERROR: DLL not found. Build the project first using Visual Studio or:"
    echo "  dotnet build -c Release"
    exit 1
fi

# Copy files to Thunderstore directory
echo "Copying files..."

# manifest.json (required)
cp manifest.json "$THUNDERSTORE_DIR/"

# README.md (required)
cp README.md "$THUNDERSTORE_DIR/"

# CHANGELOG.md (optional but recommended)
cp CHANGELOG.md "$THUNDERSTORE_DIR/"

# icon.png (required - 256x256)
if [ -f "icon.png" ]; then
    cp icon.png "$THUNDERSTORE_DIR/"
else
    echo "WARNING: icon.png not found!"
    echo "You need a 256x256 PNG icon for Thunderstore upload."
    echo "Creating placeholder..."
    # Create a simple placeholder (you should replace this with a real icon)
    convert -size 256x256 xc:#2d2d2d \
        -fill white -gravity center \
        -font DejaVu-Sans-Bold -pointsize 24 \
        -annotate +0+0 "Techtonica\nDedicated\nServer" \
        "$THUNDERSTORE_DIR/icon.png" 2>/dev/null || \
    echo "Install ImageMagick to auto-generate placeholder icon"
fi

# Plugin DLL
cp "bin/Release/TechtonicaDedicatedServer.dll" "$THUNDERSTORE_DIR/"

# Create the zip file
cd "$THUNDERSTORE_DIR"
ZIP_NAME="${PACKAGE_NAME}-${VERSION}.zip"
zip -r "../$ZIP_NAME" ./*

echo ""
echo "=========================================="
echo "Thunderstore package created:"
echo "  $OUTPUT_DIR/$ZIP_NAME"
echo ""
echo "To upload:"
echo "  1. Go to https://thunderstore.io/c/techtonica/"
echo "  2. Click 'Upload' in the top right"
echo "  3. Select: $OUTPUT_DIR/$ZIP_NAME"
echo "=========================================="
