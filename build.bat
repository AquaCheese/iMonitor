@echo off
echo Building iMonitor...

REM Restore dependencies
echo Restoring NuGet packages...
dotnet restore

if %errorlevel% neq 0 (
    echo Error: Failed to restore packages
    pause
    exit /b 1
)

REM Build the project
echo Building project...
dotnet build --configuration Release --no-restore

if %errorlevel% neq 0 (
    echo Error: Build failed
    pause
    exit /b 1
)

REM Publish self-contained executable
echo Publishing self-contained executable...
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output "dist"

if %errorlevel% neq 0 (
    echo Error: Publish failed
    pause
    exit /b 1
)

echo Build completed successfully!
echo Executable is located in: dist\iMonitor.exe
pause