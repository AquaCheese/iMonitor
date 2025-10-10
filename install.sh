#!/bin/bash

# iMonitor Installation Script
# Installs all dependencies and sets up the development environment

set -e

echo "🚀 Installing iMonitor - Extended Display Solution"
echo "=================================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

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

# Check system requirements
print_status "Checking system requirements..."

# Check Node.js
if ! command -v node &> /dev/null; then
    print_error "Node.js is not installed!"
    echo "Please install Node.js 16+ from https://nodejs.org/"
    exit 1
fi

NODE_VERSION=$(node --version | cut -d'v' -f2 | cut -d'.' -f1)
if [ "$NODE_VERSION" -lt 16 ]; then
    print_error "Node.js version $NODE_VERSION is too old. Please install Node.js 16 or later."
    exit 1
fi

print_success "Node.js $(node --version) found"

# Check npm
if ! command -v npm &> /dev/null; then
    print_error "npm is not installed!"
    exit 1
fi

print_success "npm $(npm --version) found"

# Get project root
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$PROJECT_ROOT"

print_status "Installing iMonitor in: $PROJECT_ROOT"

# Install desktop server dependencies
print_status "Installing desktop server dependencies..."
cd "$PROJECT_ROOT/desktop-server"

if [ ! -f "package.json" ]; then
    print_error "Desktop server package.json not found!"
    exit 1
fi

npm install

if [ $? -eq 0 ]; then
    print_success "Desktop server dependencies installed"
else
    print_error "Failed to install desktop server dependencies"
    exit 1
fi

# Install web client dependencies
print_status "Installing web client dependencies..."
cd "$PROJECT_ROOT/web-client"

if [ ! -f "package.json" ]; then
    print_error "Web client package.json not found!"
    exit 1
fi

npm install

if [ $? -eq 0 ]; then
    print_success "Web client dependencies installed"
else
    print_error "Failed to install web client dependencies"
    exit 1
fi

# Install mobile client dependencies (optional)
print_status "Installing mobile client dependencies..."
cd "$PROJECT_ROOT/mobile-client"

if [ ! -f "package.json" ]; then
    print_warning "Mobile client package.json not found, skipping..."
else
    npm install
    if [ $? -eq 0 ]; then
        print_success "Mobile client dependencies installed"
    else
        print_warning "Failed to install mobile client dependencies (optional)"
    fi
fi

# Return to project root
cd "$PROJECT_ROOT"

# Check for additional requirements
print_status "Checking additional requirements..."

# Check for USB support (libusb)
if command -v pkg-config &> /dev/null && pkg-config --exists libusb-1.0; then
    print_success "libusb found - USB device support enabled"
else
    print_warning "libusb not found - USB device support may be limited"
    echo "To enable full USB support, install libusb:"
    
    if [[ "$OSTYPE" == "darwin"* ]]; then
        echo "  brew install libusb"
    elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
        echo "  sudo apt-get install libusb-1.0-0-dev (Ubuntu/Debian)"
        echo "  sudo yum install libusb1-devel (CentOS/RHEL)"
    fi
fi

# Create .env file with default settings
if [ ! -f ".env" ]; then
    print_status "Creating default configuration..."
    cat > .env << EOF
# iMonitor Configuration
NODE_ENV=development
HTTP_PORT=8080
WS_PORT=8081
DEFAULT_QUALITY=80
DEFAULT_FPS=30
ENABLE_USB=true
LOG_LEVEL=info
EOF
    print_success "Configuration file created (.env)"
fi

# Create startup script
print_status "Creating startup script..."
cat > start.sh << 'EOF'
#!/bin/bash
echo "Starting iMonitor Desktop Server..."
cd desktop-server
npm start
EOF

chmod +x start.sh
print_success "Startup script created (start.sh)"

# Installation complete
print_success "🎉 Installation completed successfully!"

echo ""
echo "📋 Next Steps:"
echo "=============="
echo "1. Start the desktop server:"
echo "   ./start.sh"
echo "   or"
echo "   cd desktop-server && npm start"
echo ""
echo "2. For USB-C iPad connection:"
echo "   - Connect your iPad via USB-C cable"
echo "   - The app will automatically detect it"
echo "   - Select display and click 'Start Streaming'"
echo ""
echo "3. For wireless connection:"
echo "   - Open the QR code or URL on your mobile device"
echo "   - Use the web client or install the mobile app"
echo ""
echo "4. Build for production:"
echo "   ./build.sh"
echo ""

# Platform-specific notes
if [[ "$OSTYPE" == "darwin"* ]]; then
    echo "macOS Notes:"
    echo "- For USB support, you may need to install additional drivers"
    echo "- Grant screen recording permissions when prompted"
    echo "- iOS app development requires Xcode"
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    echo "Linux Notes:"
    echo "- You may need to run with sudo for USB device access"
    echo "- Install additional packages: sudo apt-get install libudev-dev"
    echo "- Add user to dialout group: sudo usermod -a -G dialout \$USER"
fi

echo ""
print_status "Ready to transform your mobile devices into extended displays! 🖥️📱"