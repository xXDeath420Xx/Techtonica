#!/bin/bash
set -e

echo "setup"
rm -rf libs
mkdir -p libs/Managed
# cp -r unity/build/unity_Data/Plugins libs
# cp -r unity/build/unity_Data/StreamingAssets libs
# cp -r unity/build/unity_Data/UnitySubsystems libs

# Build the project
echo "Building the project..."
cd plugin
dotnet build
cd ..

# Copy the mod dll to the mods folder
echo "Copying the mod dll to the mods folder..."
cp ./plugin/bin/Debug/netstandard2.1/console_commands.* /c/Program\ Files\ \(x86\)/Steam/steamapps/common/Techtonica/BepInEx/plugins/

echo "you're done it!! :3"