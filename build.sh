#!/bin/bash

echo "Building iMonitor..."

# Restore dependencies
echo "Restoring NuGet packages..."
dotnet restore

if [ $? -ne 0 ]; then
    echo "Error: Failed to restore packages"
    exit 1
fi

# Build the project
echo "Building project..."
dotnet build --configuration Release --no-restore

if [ $? -ne 0 ]; then
    echo "Error: Build failed"
    exit 1
fi

# Publish self-contained executable
echo "Publishing self-contained executable..."
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output "dist"

if [ $? -ne 0 ]; then
    echo "Error: Publish failed"
    exit 1
fi

echo "Build completed successfully!"
echo "Executable is located in: dist/iMonitor.exe"