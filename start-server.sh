#!/bin/bash
# Techtonica Dedicated Server Startup Script

# Check if Xvfb is running
if ! pgrep -x Xvfb > /dev/null; then
    echo "Starting Xvfb on display :98..."
    Xvfb :98 -screen 0 1024x768x24 &
    sleep 2
fi

export DISPLAY=:98
export WINEPREFIX=/home/death/techtonica-server/wine/prefix
export WINEDLLOVERRIDES="winhttp=n,b"

cd /home/death/techtonica-server/game/Techtonica

echo "Starting Techtonica server..."
echo "Press Ctrl+C to stop"

wine Techtonica.exe -batchmode -logfile /home/death/techtonica-server/game/Techtonica/game.log

echo "Server stopped"
