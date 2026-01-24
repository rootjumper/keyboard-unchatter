#!/bin/bash

# Keyboard Unchatter Linux Installation Script

set -e

echo "=================================="
echo "Keyboard Unchatter Linux Installer"
echo "=================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if running as root
if [ "$EUID" -eq 0 ]; then
    echo -e "${RED}ERROR: Do not run this script as root!${NC}"
    echo "This script will prompt for sudo when needed."
    exit 1
fi

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}ERROR: .NET is not installed!${NC}"
    echo "Please install .NET 8.0 or later:"
    echo "  Ubuntu/Debian: https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu"
    echo "  Fedora: https://docs.microsoft.com/en-us/dotnet/core/install/linux-fedora"
    echo "  Arch: sudo pacman -S dotnet-sdk"
    exit 1
fi

echo -e "${GREEN}✓ .NET is installed${NC}"

# Check .NET version
DOTNET_VERSION=$(dotnet --version | cut -d'.' -f1)
if [ "$DOTNET_VERSION" -lt 6 ]; then
    echo -e "${YELLOW}WARNING: .NET version may be too old. Please install .NET 6.0 or later.${NC}"
fi

# Build the application
echo ""
echo "Building Keyboard Unchatter..."
dotnet build -c Release

if [ $? -ne 0 ]; then
    echo -e "${RED}ERROR: Build failed!${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Build successful${NC}"

# Create installation directory
INSTALL_DIR="$HOME/.local/bin"
mkdir -p "$INSTALL_DIR"

# Copy the executable
echo ""
echo "Installing to $INSTALL_DIR..."
cp bin/Release/net8.0/KeyboardUnchatter.Linux "$INSTALL_DIR/"
cp bin/Release/net8.0/KeyboardUnchatter.Linux.dll "$INSTALL_DIR/"
cp bin/Release/net8.0/*.so "$INSTALL_DIR/" 2>/dev/null || true
cp -r bin/Release/net8.0/runtimes "$INSTALL_DIR/" 2>/dev/null || true

# Make executable
chmod +x "$INSTALL_DIR/KeyboardUnchatter.Linux"

echo -e "${GREEN}✓ Installed to $INSTALL_DIR${NC}"

# Check if user is in input group
if ! groups | grep -q input; then
    echo ""
    echo -e "${YELLOW}WARNING: Your user is not in the 'input' group!${NC}"
    echo "To allow the application to access keyboard devices, run:"
    echo "  sudo usermod -a -G input $USER"
    echo "Then log out and log back in."
    echo ""
    read -p "Do you want to add your user to the 'input' group now? (y/n) " -n 1 -r
    echo ""
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        sudo usermod -a -G input $USER
        echo -e "${GREEN}✓ Added to input group. Please log out and log back in.${NC}"
    fi
else
    echo -e "${GREEN}✓ User is in input group${NC}"
fi

# Check if uinput module is loaded
if ! lsmod | grep -q uinput; then
    echo ""
    echo -e "${YELLOW}WARNING: uinput kernel module is not loaded!${NC}"
    echo "This module is required for event injection."
    echo ""
    read -p "Do you want to load the uinput module now? (y/n) " -n 1 -r
    echo ""
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        sudo modprobe uinput
        echo -e "${GREEN}✓ uinput module loaded${NC}"
        
        # Make it persistent
        echo "Making uinput load at boot..."
        echo "uinput" | sudo tee /etc/modules-load.d/uinput.conf > /dev/null
        echo -e "${GREEN}✓ uinput will load at boot${NC}"
    fi
fi

# Create udev rule for uinput access
echo ""
read -p "Create udev rule for uinput access? (y/n) " -n 1 -r
echo ""
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo 'KERNEL=="uinput", GROUP="input", MODE="0660"' | sudo tee /etc/udev/rules.d/99-uinput.rules > /dev/null
    sudo udevadm control --reload-rules
    sudo udevadm trigger
    echo -e "${GREEN}✓ udev rule created${NC}"
fi

# Create desktop entry
echo ""
read -p "Create desktop menu entry? (y/n) " -n 1 -r
echo ""
if [[ $REPLY =~ ^[Yy]$ ]]; then
    DESKTOP_DIR="$HOME/.local/share/applications"
    mkdir -p "$DESKTOP_DIR"
    
    cat > "$DESKTOP_DIR/keyboard-unchatter.desktop" << EOF
[Desktop Entry]
Type=Application
Name=Keyboard Unchatter
Comment=Fix keyboard chattering on Linux
Exec=$INSTALL_DIR/KeyboardUnchatter.Linux
Icon=input-keyboard
Terminal=false
Categories=Utility;System;
Keywords=keyboard;chatter;debounce;
StartupNotify=false
EOF
    
    chmod +x "$DESKTOP_DIR/keyboard-unchatter.desktop"
    echo -e "${GREEN}✓ Desktop entry created${NC}"
fi

# Add to PATH if needed
if [[ ":$PATH:" != *":$INSTALL_DIR:"* ]]; then
    echo ""
    echo -e "${YELLOW}NOTE: $INSTALL_DIR is not in your PATH${NC}"
    echo "You can add it by adding this line to your ~/.bashrc or ~/.profile:"
    echo "  export PATH=\"\$HOME/.local/bin:\$PATH\""
fi

echo ""
echo -e "${GREEN}=================================="
echo "Installation complete!"
echo "==================================${NC}"
echo ""
echo "To run Keyboard Unchatter, use:"
echo "  $INSTALL_DIR/KeyboardUnchatter.Linux"
echo ""
echo "Or search for 'Keyboard Unchatter' in your application menu."
echo ""
echo -e "${YELLOW}IMPORTANT:${NC} If you just added your user to the 'input' group,"
echo "you MUST log out and log back in for the changes to take effect!"
echo ""
