@echo off
setlocal enabledelayedexpansion

REM =====================================================
REM Techtonica Dedicated Server - Windows Setup
REM CertiFried Community - https://certifriedmultitool.com
REM =====================================================

title Techtonica Dedicated Server Setup

echo.
echo ============================================================
echo       Techtonica Dedicated Server - Windows Installation
echo            CertiFried Community Server Solution
echo ============================================================
echo.

REM Get installation directory
set "INSTALL_DIR=%~dp0"
set "GAME_DIR=%INSTALL_DIR%Techtonica"
set "CONFIG_DIR=%INSTALL_DIR%config"
set "SAVES_DIR=%INSTALL_DIR%saves"

echo Installation directory: %INSTALL_DIR%
echo.

REM Check if game files exist
if not exist "%GAME_DIR%\Techtonica.exe" (
    echo [!] Game files not found!
    echo.
    echo Please copy your Techtonica game files to:
    echo   %GAME_DIR%
    echo.
    echo Required files:
    echo   - Techtonica.exe
    echo   - Techtonica_Data\ folder
    echo   - MonoBleedingEdge\ folder
    echo   - UnityCrashHandler64.exe
    echo.
    echo You can find these in your Steam library:
    echo   Right-click Techtonica ^> Properties ^> Local Files ^> Browse
    echo.
    pause
    exit /b 1
)

echo [OK] Game files found
echo.

REM Create directories
if not exist "%CONFIG_DIR%" mkdir "%CONFIG_DIR%"
if not exist "%SAVES_DIR%" mkdir "%SAVES_DIR%"
if not exist "%GAME_DIR%\BepInEx\plugins" mkdir "%GAME_DIR%\BepInEx\plugins"
if not exist "%GAME_DIR%\BepInEx\patchers" mkdir "%GAME_DIR%\BepInEx\patchers"
if not exist "%GAME_DIR%\BepInEx\config" mkdir "%GAME_DIR%\BepInEx\config"

echo [OK] Directories created
echo.

REM Check/Install BepInEx
if not exist "%GAME_DIR%\BepInEx\core\BepInEx.dll" (
    echo [*] Installing BepInEx...

    if exist "%INSTALL_DIR%BepInEx_win_x64.zip" (
        echo     Extracting BepInEx...
        powershell -Command "Expand-Archive -Path '%INSTALL_DIR%BepInEx_win_x64.zip' -DestinationPath '%GAME_DIR%' -Force"
        echo [OK] BepInEx installed
    ) else (
        echo [!] BepInEx_win_x64.zip not found!
        echo     Please download BepInEx from:
        echo     https://github.com/BepInEx/BepInEx/releases
        echo.
        echo     Extract to: %GAME_DIR%
        pause
        exit /b 1
    )
) else (
    echo [OK] BepInEx already installed
)
echo.

REM Install mods
echo [*] Installing server mods...

if exist "%INSTALL_DIR%mods\TechtonicaPreloader.dll" (
    copy /y "%INSTALL_DIR%mods\TechtonicaPreloader.dll" "%GAME_DIR%\BepInEx\patchers\" >nul
    echo [OK] TechtonicaPreloader installed
) else (
    echo [!] TechtonicaPreloader.dll not found in mods folder
)

if exist "%INSTALL_DIR%mods\TechtonicaDedicatedServer.dll" (
    copy /y "%INSTALL_DIR%mods\TechtonicaDedicatedServer.dll" "%GAME_DIR%\BepInEx\plugins\" >nul
    echo [OK] TechtonicaDedicatedServer installed
) else (
    echo [!] TechtonicaDedicatedServer.dll not found in mods folder
)
echo.

REM Create default config
set "CONFIG_FILE=%GAME_DIR%\BepInEx\config\com.community.techtonicadedicatedserver.cfg"
if not exist "%CONFIG_FILE%" (
    echo [*] Creating default configuration...
    (
        echo ## Settings file was created by plugin Techtonica Dedicated Server v0.1.0
        echo ## Plugin GUID: com.community.techtonicadedicatedserver
        echo.
        echo [Server]
        echo.
        echo ## Enable direct IP connections ^(bypasses Steam^)
        echo EnableDirectConnect = true
        echo.
        echo ## Server port for direct connections
        echo ServerPort = 6968
        echo.
        echo ## Maximum number of players
        echo MaxPlayers = 8
        echo.
        echo ## Enable auto-start on launch
        echo AutoStart = true
        echo.
        echo ## Save file path for auto-load ^(leave empty to start fresh^)
        echo AutoLoadSavePath =
        echo.
        echo [Debug]
        echo.
        echo ## Enable verbose logging
        echo VerboseLogging = false
    ) > "%CONFIG_FILE%"
    echo [OK] Default config created
) else (
    echo [OK] Config already exists
)
echo.

REM Create startup batch files
echo [*] Creating startup scripts...

REM Main startup script
(
    echo @echo off
    echo title Techtonica Dedicated Server
    echo.
    echo echo Starting Techtonica Dedicated Server...
    echo echo Press Ctrl+C to stop
    echo echo.
    echo.
    echo cd /d "%%~dp0Techtonica"
    echo.
    echo set DOORSTOP_ENABLE=TRUE
    echo set DOORSTOP_INVOKE_DLL_PATH=BepInEx\core\BepInEx.Preloader.dll
    echo.
    echo Techtonica.exe -batchmode -logfile "%%~dp0server.log"
    echo.
    echo echo.
    echo echo Server stopped.
    echo pause
) > "%INSTALL_DIR%start-server.bat"

REM Start without console window (for background running)
(
    echo @echo off
    echo start /min "" "%%~dp0start-server.bat"
) > "%INSTALL_DIR%start-server-background.bat"

echo [OK] Startup scripts created
echo.

REM Summary
echo ============================================================
echo            Installation Complete!
echo ============================================================
echo.
echo Installation directory: %INSTALL_DIR%
echo.
echo To start the server:
echo   Run: start-server.bat
echo.
echo To run in background:
echo   Run: start-server-background.bat
echo.
echo Server will listen on port 6968 (UDP)
echo Make sure to open this port in Windows Firewall!
echo.
echo Configuration file:
echo   %CONFIG_FILE%
echo.
echo Logs:
echo   %INSTALL_DIR%server.log
echo   %GAME_DIR%\BepInEx\LogOutput.log
echo.
echo ============================================================
echo.
pause
