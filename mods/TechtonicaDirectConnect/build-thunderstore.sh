#!/bin/bash
# Build script for Thunderstore package

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "Building TechtonicaDirectConnect..."

# Build the project
dotnet build -c Release

# Create thunderstore package
PACKAGE_DIR="$SCRIPT_DIR/thunderstore-package"
rm -rf "$PACKAGE_DIR"
mkdir -p "$PACKAGE_DIR"

# Copy files
cp "bin/TechtonicaDirectConnect.dll" "$PACKAGE_DIR/"
cp "thunderstore/manifest.json" "$PACKAGE_DIR/"
cp "thunderstore/README.md" "$PACKAGE_DIR/"

# Copy or create icon
if [ -f "thunderstore/icon.png" ]; then
    cp "thunderstore/icon.png" "$PACKAGE_DIR/"
elif [ -f "../TechtonicaDedicatedServer/thunderstore/icon.png" ]; then
    cp "../TechtonicaDedicatedServer/thunderstore/icon.png" "$PACKAGE_DIR/"
else
    echo "Warning: No icon.png found. Creating placeholder..."
    # Create a simple placeholder (you should replace with actual icon)
    convert -size 256x256 xc:'#7c3aed' -fill white -gravity center \
        -font DejaVu-Sans-Bold -pointsize 48 -annotate 0 'DC' \
        "$PACKAGE_DIR/icon.png" 2>/dev/null || echo "Could not create icon"
fi

# Get version from manifest
VERSION=$(grep -oP '"version_number":\s*"\K[^"]+' thunderstore/manifest.json)

# Create zip
cd "$PACKAGE_DIR"
zip -r "../TechtonicaDirectConnect-${VERSION}.zip" .

echo ""
echo "Package created: TechtonicaDirectConnect-${VERSION}.zip"
echo ""
echo "To upload to Thunderstore:"
echo "1. Go to https://thunderstore.io/c/techtonica/create/"
echo "2. Upload TechtonicaDirectConnect-${VERSION}.zip"
