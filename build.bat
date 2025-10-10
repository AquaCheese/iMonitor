@echo off
REM iMonitor Build Script for Windows
REM Builds all components of the iMonitor application

echo Building iMonitor - Extended Display Solution
echo ================================================

REM Check if Node.js is installed
node --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Node.js is not installed. Please install Node.js 16+ and try again.
    exit /b 1
)

REM Check if npm is installed
npm --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] npm is not installed. Please install npm and try again.
    exit /b 1
)

REM Get the project root directory
set PROJECT_ROOT=%~dp0
cd /d "%PROJECT_ROOT%"

echo [INFO] Project root: %PROJECT_ROOT%

REM Build desktop server
echo [INFO] Building desktop server...
cd "%PROJECT_ROOT%desktop-server"

if not exist "package.json" (
    echo [ERROR] Desktop server package.json not found!
    exit /b 1
)

echo [INFO] Installing desktop server dependencies...
call npm install
if %errorlevel% neq 0 (
    echo [ERROR] Failed to install desktop server dependencies
    exit /b 1
)

echo [INFO] Building desktop server executable...
call npm run build
if %errorlevel% neq 0 (
    echo [ERROR] Failed to build desktop server
    exit /b 1
)

echo [SUCCESS] Desktop server built successfully!

REM Build web client
echo [INFO] Building web client...
cd "%PROJECT_ROOT%web-client"

if not exist "package.json" (
    echo [ERROR] Web client package.json not found!
    exit /b 1
)

echo [INFO] Installing web client dependencies...
call npm install
if %errorlevel% neq 0 (
    echo [ERROR] Failed to install web client dependencies
    exit /b 1
)

echo [INFO] Building web client...
call npm run build
if %errorlevel% neq 0 (
    echo [ERROR] Failed to build web client
    exit /b 1
)

echo [SUCCESS] Web client built successfully!

REM Create distribution directory
echo [INFO] Creating distribution package...
set DIST_DIR=%PROJECT_ROOT%dist
if not exist "%DIST_DIR%" mkdir "%DIST_DIR%"

REM Copy built files
if exist "%PROJECT_ROOT%desktop-server\dist" (
    xcopy "%PROJECT_ROOT%desktop-server\dist\*" "%DIST_DIR%\" /E /Y
    echo [SUCCESS] Desktop server files copied to dist\
)

if exist "%PROJECT_ROOT%web-client\dist" (
    if not exist "%DIST_DIR%\web-client" mkdir "%DIST_DIR%\web-client"
    xcopy "%PROJECT_ROOT%web-client\dist\*" "%DIST_DIR%\web-client\" /E /Y
    echo [SUCCESS] Web client files copied to dist\web-client\
)

REM Create README for distribution
echo iMonitor - Extended Display Solution > "%DIST_DIR%\README.txt"
echo =================================== >> "%DIST_DIR%\README.txt"
echo. >> "%DIST_DIR%\README.txt"
echo This package contains the built iMonitor application. >> "%DIST_DIR%\README.txt"
echo. >> "%DIST_DIR%\README.txt"
echo Desktop Server: >> "%DIST_DIR%\README.txt"
echo - Run the .exe installer to install the desktop application >> "%DIST_DIR%\README.txt"
echo - The desktop server will start and show a system tray icon >> "%DIST_DIR%\README.txt"
echo - Click "Start Streaming" to begin broadcasting your screen >> "%DIST_DIR%\README.txt"
echo. >> "%DIST_DIR%\README.txt"
echo Web Client: >> "%DIST_DIR%\README.txt"
echo - The web client is served automatically by the desktop server >> "%DIST_DIR%\README.txt"
echo - Mobile devices can connect by scanning the QR code >> "%DIST_DIR%\README.txt"
echo. >> "%DIST_DIR%\README.txt"
echo Version: 1.0.0 >> "%DIST_DIR%\README.txt"

echo [SUCCESS] Build completed successfully!
echo [INFO] Distribution files are available in: %DIST_DIR%

echo.
echo Build Summary
echo =============
echo Desktop Server: Built and packaged
echo Web Client: Built and ready for deployment
echo Mobile Client: Source code ready (requires separate build)
echo Distribution: Available in dist\ directory
echo.

echo [SUCCESS] iMonitor build complete! Ready to transform mobile devices into extended displays.
pause