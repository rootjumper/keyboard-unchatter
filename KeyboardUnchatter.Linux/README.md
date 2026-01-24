# Keyboard Unchatter - Linux Version

Linux-compatible version of Keyboard Unchatter that provides keyboard chatter filtering functionality using native Linux input subsystem.

## Features

- **Keyboard Chatter Detection**: Filters repeated keypresses within a configurable threshold (default 50ms)
- **Multiple Keyboard Support**: Automatically detects and monitors all connected keyboard devices
- **GTK+ GUI**: Native Linux interface with status monitoring and configuration
- **Key Statistics**: Track press count, chatter count, and failure rate per key
- **Typing Speed Diagnostics**: Real-time typing speed analysis
- **Persistent Configuration**: XDG-compliant configuration storage (~/.config/keyboard-unchatter/)
- **Auto-start Support**: Optional automatic launch on system startup

## Requirements

### System Requirements
- Linux kernel 2.6.36 or later (for evdev support)
- .NET 8.0 or later runtime
- GTK+ 3.0 or later
- Access to `/dev/input` devices (via `input` group membership or elevated privileges)

### Supported Distributions
Tested on:
- Ubuntu 22.04+
- Fedora 38+
- Debian 12+
- Arch Linux

Should work on most modern Linux distributions with evdev support.

### Desktop Environments
- GNOME
- KDE Plasma
- XFCE
- MATE
- Cinnamon
- Other X11/Wayland-based environments

## Installation

### Prerequisites

1. **Install .NET Runtime** (if not already installed):

   **Ubuntu/Debian:**
   ```bash
   wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
   chmod +x dotnet-install.sh
   ./dotnet-install.sh --channel 8.0
   ```

   **Fedora:**
   ```bash
   sudo dnf install dotnet-sdk-8.0
   ```

   **Arch Linux:**
   ```bash
   sudo pacman -S dotnet-sdk
   ```

2. **Install GTK+ 3** (usually pre-installed on most distributions):

   **Ubuntu/Debian:**
   ```bash
   sudo apt install libgtk-3-0
   ```

   **Fedora:**
   ```bash
   sudo dnf install gtk3
   ```

   **Arch Linux:**
   ```bash
   sudo pacman -S gtk3
   ```

### Quick Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/rootjumper/keyboard-unchatter.git
   cd keyboard-unchatter/KeyboardUnchatter.Linux
   ```

2. Run the installation script:
   ```bash
   ./install.sh
   ```

   The script will:
   - Build the application
   - Install it to `~/.local/bin`
   - Optionally add your user to the `input` group
   - Load and configure the `uinput` kernel module
   - Create desktop menu entry

3. **Important**: If you added your user to the `input` group, you must **log out and log back in** for the changes to take effect.

### Manual Installation

1. Build the application:
   ```bash
   dotnet build -c Release
   ```

2. Copy the executable:
   ```bash
   mkdir -p ~/.local/bin
   cp bin/Release/net8.0/KeyboardUnchatter.Linux ~/.local/bin/
   ```

3. Add your user to the input group:
   ```bash
   sudo usermod -a -G input $USER
   ```
   Then log out and log back in.

4. Load the uinput module:
   ```bash
   sudo modprobe uinput
   echo "uinput" | sudo tee /etc/modules-load.d/uinput.conf
   ```

## Permissions Setup

The application needs access to `/dev/input/eventX` devices to intercept keyboard events. There are several ways to grant this access:

### Option 1: Add User to Input Group (Recommended)

```bash
sudo usermod -a -G input $USER
```

Then **log out and log back in** for the changes to take effect.

### Option 2: Create udev Rules

Create `/etc/udev/rules.d/99-keyboard-unchatter.rules`:
```
KERNEL=="event*", SUBSYSTEM=="input", GROUP="input", MODE="0660"
KERNEL=="uinput", GROUP="input", MODE="0660"
```

Reload udev rules:
```bash
sudo udevadm control --reload-rules
sudo udevadm trigger
```

### Option 3: Run with Elevated Privileges (Not Recommended)

```bash
sudo ~/.local/bin/KeyboardUnchatter.Linux
```

**Note**: Running with sudo is not recommended for desktop applications due to security implications.

## Usage

### Starting the Application

From terminal:
```bash
~/.local/bin/KeyboardUnchatter.Linux
```

Or search for "Keyboard Unchatter" in your application menu.

### GUI Overview

**Status Panel:**
- Shows current status (Active/Inactive)
- Displays real-time typing speed diagnostics
- Activate/Deactivate buttons to control filtering

**Settings Panel:**
- **Chatter Threshold**: Adjust the time window (in milliseconds) for detecting chatter. Lower values are more aggressive.
- **Launch minimized**: Start the application in the background
- **Activate on launch**: Automatically enable filtering when the application starts
- **Run at startup**: Automatically start the application when you log in

**Key Statistics:**
- Shows per-key statistics: press count, chatter count, and failure rate
- Reset button to clear all statistics

### Configuration

Configuration is stored in `~/.config/keyboard-unchatter/settings.json` and follows XDG Base Directory specification.

Example configuration:
```json
{
  "ChatterThreshold": 50.0,
  "OpenMinimized": false,
  "ActivateOnLaunch": true,
  "RunAtStartup": false
}
```

## Troubleshooting

### "Cannot access input devices" Error

**Problem**: Application cannot read from `/dev/input/eventX` devices.

**Solutions**:
1. Check if your user is in the `input` group:
   ```bash
   groups | grep input
   ```
2. If not, add your user to the group:
   ```bash
   sudo usermod -a -G input $USER
   ```
3. Log out and log back in for changes to take effect.

### "No keyboard devices found" Warning

**Problem**: Application cannot find any keyboard devices.

**Solutions**:
1. Check if keyboard devices exist:
   ```bash
   ls -l /dev/input/event*
   ```
2. Check device capabilities:
   ```bash
   sudo evtest
   ```
3. Ensure you have permission to read the devices (see above).

### uinput Module Not Loaded

**Problem**: "Could not open /dev/uinput" warning.

**Solution**:
```bash
sudo modprobe uinput
echo "uinput" | sudo tee /etc/modules-load.d/uinput.conf
```

### Application Doesn't Start on Login

**Problem**: Auto-start not working.

**Solutions**:
1. Check if desktop entry exists:
   ```bash
   ls ~/.config/autostart/keyboard-unchatter.desktop
   ```
2. Verify the entry points to the correct executable path
3. Check desktop environment logs for errors

### Key Events Not Being Filtered

**Problem**: Chatter is not being blocked.

**Solutions**:
1. Ensure the application is **Activated** (green status)
2. Adjust the chatter threshold - try increasing it
3. Check console output for debugging information
4. Verify the application has grabbed the keyboard devices

### Wayland Compatibility

**Note**: The application uses evdev directly, which works on both X11 and Wayland. However, on Wayland, some desktop environments may restrict access to input devices for security reasons.

If experiencing issues on Wayland:
1. Try running on X11 session instead
2. Check Wayland compositor logs for security policy violations
3. Some compositors may require additional configuration

## Known Limitations

1. **Wayland Security**: Some Wayland compositors may restrict input device access
2. **Multiple Keyboards**: While supported, configuration is global (not per-keyboard)
3. **Gaming**: Not recommended for use during gaming - may cause input lag or unexpected behavior
4. **Key Up Events**: In rare cases, blocked key-up events may cause stuck keys (restart application to reset)

## Performance Notes

- The application runs with minimal CPU and memory overhead
- Event processing happens in real-time with sub-millisecond latency
- Typing speed tracking maintains only the last 50 keypress intervals

## Security Considerations

- The application requires access to keyboard input devices, which means it can read all keyboard input
- Configuration files are stored in user-readable format in `~/.config/`
- No network access or external communication
- Input events are processed locally and never transmitted
- Source code is open and auditable

## Uninstallation

To remove Keyboard Unchatter:

```bash
rm ~/.local/bin/KeyboardUnchatter.Linux*
rm ~/.local/share/applications/keyboard-unchatter.desktop
rm ~/.config/autostart/keyboard-unchatter.desktop
rm -rf ~/.config/keyboard-unchatter/
sudo gpasswd -d $USER input  # Optional: remove from input group
```

## Building from Source

```bash
cd KeyboardUnchatter.Linux
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

## Development

### Dependencies
- .NET 8.0 SDK
- GtkSharp (3.24.24.95)
- Newtonsoft.Json (13.0.3)
- Tmds.Linux (0.7.0)

### Project Structure
```
KeyboardUnchatter.Linux/
├── Program.cs              # Entry point
├── LinuxInputHook.cs      # evdev-based input handling
├── KeyboardMonitor.cs     # Chatter detection logic
├── KeyStatusList.cs       # Key status tracking
├── MainWindow.cs          # GTK+ GUI
├── Configuration/
│   └── Settings.cs        # Configuration management
├── systemd/
│   └── keyboard-unchatter.service  # Systemd service
└── install.sh             # Installation script
```

## Contributing

Contributions are welcome! Please see the main repository for contribution guidelines.

## License

See LICENSE file in the root of the repository.

## Support

For issues, questions, or feature requests, please open an issue on GitHub:
https://github.com/rootjumper/keyboard-unchatter/issues

## Acknowledgments

- Original Windows version by ZoserLock
- Linux port by the Keyboard Unchatter community
- Built with GtkSharp and .NET
