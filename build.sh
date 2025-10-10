#!/bin/bash

# iMonitor Build Script
# Builds all components of the iMonitor application

set -e

echo "🚀 Building iMonitor - Extended Display Solution"
echo "================================================"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if Node.js is installed
if ! command -v node &> /dev/null; then
    print_error "Node.js is not installed. Please install Node.js 16+ and try again."
    exit 1
fi

# Check if npm is installed
if ! command -v npm &> /dev/null; then
    print_error "npm is not installed. Please install npm and try again."
    exit 1
fi

# Get the project root directory
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$PROJECT_ROOT"

print_status "Project root: $PROJECT_ROOT"

# Build desktop server
print_status "Building desktop server..."
cd "$PROJECT_ROOT/desktop-server"

if [ ! -f "package.json" ]; then
    print_error "Desktop server package.json not found!"
    exit 1
fi

print_status "Installing desktop server dependencies..."
npm install

print_status "Building desktop server executable..."
npm run build

if [ $? -eq 0 ]; then
    print_success "Desktop server built successfully!"
else
    print_error "Failed to build desktop server"
    exit 1
fi

# Build web client
print_status "Building web client..."
cd "$PROJECT_ROOT/web-client"

if [ ! -f "package.json" ]; then
    print_error "Web client package.json not found!"
    exit 1
fi

print_status "Installing web client dependencies..."
npm install

print_status "Building web client..."
npm run build

if [ $? -eq 0 ]; then
    print_success "Web client built successfully!"
else
    print_error "Failed to build web client"
    exit 1
fi

# Mobile client instructions
print_status "Mobile client build instructions:"
print_warning "React Native mobile app requires additional setup:"
echo "  1. Install React Native CLI: npm install -g @react-native-community/cli"
echo "  2. For Android: Install Android Studio and configure Android SDK"
echo "  3. For iOS: Install Xcode (macOS only)"
echo "  4. Navigate to mobile-client directory"
echo "  5. Run 'npm install' to install dependencies"
echo "  6. For Android: 'npm run build-android'"
echo "  7. For iOS: 'npm run build-ios' (macOS only)"

# Create distribution directory
print_status "Creating distribution package..."
DIST_DIR="$PROJECT_ROOT/dist"
mkdir -p "$DIST_DIR"

# Copy built files
if [ -d "$PROJECT_ROOT/desktop-server/dist" ]; then
    cp -r "$PROJECT_ROOT/desktop-server/dist/"* "$DIST_DIR/"
    print_success "Desktop server files copied to dist/"
fi

if [ -d "$PROJECT_ROOT/web-client/dist" ]; then
    mkdir -p "$DIST_DIR/web-client"
    cp -r "$PROJECT_ROOT/web-client/dist/"* "$DIST_DIR/web-client/"
    print_success "Web client files copied to dist/web-client/"
fi

# Create README for distribution
cat > "$DIST_DIR/README.txt" << EOF
iMonitor - Extended Display Solution
===================================

This package contains the built iMonitor application.

Desktop Server:
- Run the executable for your platform (Windows .exe, macOS .app, Linux AppImage)
- The desktop server will start and show a system tray icon
- Click "Start Streaming" to begin broadcasting your screen

Web Client:
- The web client is served automatically by the desktop server
- Mobile devices can connect by scanning the QR code or navigating to the displayed URL
- Files are also available in the web-client/ directory for custom hosting

Mobile Apps:
- Native iOS and Android apps need to be built separately
- Source code is available in the mobile-client/ directory
- Requires React Native development environment

Support:
- Visit the project repository for documentation and support
- Report issues on GitHub

Version: 1.0.0
Build Date: $(date)
EOF

print_success "Build completed successfully!"
print_status "Distribution files are available in: $DIST_DIR"

# Display build summary
echo ""
echo "📦 Build Summary"
echo "================"
echo "✅ Desktop Server: Built and packaged"
echo "✅ Web Client: Built and ready for deployment"
echo "ℹ️  Mobile Client: Source code ready (requires separate build)"
echo "📁 Distribution: Available in dist/ directory"
echo ""

# Platform-specific instructions
if [[ "$OSTYPE" == "darwin"* ]]; then
    print_status "macOS detected - Desktop app should be in dist/ as .app bundle"
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    print_status "Linux detected - Desktop app should be in dist/ as AppImage"
elif [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
    print_status "Windows detected - Desktop app should be in dist/ as .exe installer"
fi

print_success "🎉 iMonitor build complete! Ready to transform mobile devices into extended displays."