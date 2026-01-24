# Testing Guide for Keyboard Unchatter Linux

## Manual Testing Checklist

Since this application requires actual hardware keyboard interaction and Linux system permissions, automated unit testing is limited. Use this manual testing guide to verify functionality.

### Prerequisites

Before testing, ensure:
1. You are running on a Linux system
2. .NET 8.0 or later is installed
3. GTK+ 3 is installed
4. Your user is in the `input` group OR you can run with sudo
5. `uinput` kernel module is loaded

### Build and Run

```bash
cd KeyboardUnchatter.Linux
dotnet build -c Release
cd bin/Release/net8.0
./KeyboardUnchatter.Linux
```

## Test Plan

### 1. Application Startup Test
- [ ] Application starts without errors
- [ ] Main window displays correctly
- [ ] Status shows "Status: Inactive"
- [ ] All UI controls are visible and properly laid out

### 2. Permission and Device Detection Test
Check console output for:
- [ ] "✓ Input device access: OK" message
- [ ] List of detected keyboard devices
- [ ] No critical permission errors

If permission errors:
```bash
sudo usermod -a -G input $USER
# Log out and log back in
```

### 3. Chatter Detection Test

#### Setup:
1. Open a text editor (gedit, kate, or any)
2. Place cursor in the editor
3. In Keyboard Unchatter, click **Activate**
4. Set threshold to 50ms (default)

#### Test Cases:

**Test 3a: Normal Typing**
- [ ] Type normally: "The quick brown fox"
- [ ] All characters appear correctly
- [ ] No characters are duplicated
- [ ] Statistics show press counts increasing

**Test 3b: Rapid Key Presses (Simulated Chatter)**
- [ ] Press and quickly tap a single key (like 'a') multiple times rapidly
- [ ] Some presses should be blocked (shown in statistics)
- [ ] Failure rate should be > 0%
- [ ] Typing speed diagnostic shows intervals

**Test 3c: Threshold Adjustment**
- [ ] Change threshold to 100ms
- [ ] Try rapid typing again
- [ ] More keys should be blocked with higher threshold
- [ ] Change threshold to 20ms
- [ ] Fewer keys should be blocked with lower threshold

### 4. Statistics Test
- [ ] Key statistics table populates as you type
- [ ] Press Count increases with each keypress
- [ ] Chatter Count increases when keys are blocked
- [ ] Failure Rate calculates correctly (ChatterCount/PressCount * 100)
- [ ] Click **Reset Statistics** button
- [ ] All statistics clear to zero

### 5. Typing Speed Diagnostics Test
- [ ] Type several words at normal pace
- [ ] "Typing speed" label updates with median interval
- [ ] Value is reasonable (typically 100-300ms for normal typing)
- [ ] Stops updating when application is deactivated

### 6. Settings Persistence Test
- [ ] Change chatter threshold to 75ms
- [ ] Check "Launch minimized"
- [ ] Check "Activate on launch"
- [ ] Close the application
- [ ] Reopen the application
- [ ] Verify all settings persisted:
  - [ ] Threshold is still 75ms
  - [ ] Checkboxes retain their state
- [ ] Check file exists: `~/.config/keyboard-unchatter/settings.json`

### 7. Auto-start Test
- [ ] Check "Run at startup" checkbox
- [ ] Verify desktop entry created: `~/.config/autostart/keyboard-unchatter.desktop`
- [ ] Uncheck "Run at startup"
- [ ] Verify desktop entry removed

### 8. Activate/Deactivate Test
- [ ] Click **Activate**
  - [ ] Status changes to "Status: Active"
  - [ ] Activate button becomes disabled
  - [ ] Deactivate button becomes enabled
- [ ] Type in text editor - filtering should work
- [ ] Click **Deactivate**
  - [ ] Status changes to "Status: Inactive"
  - [ ] Deactivate button becomes disabled
  - [ ] Activate button becomes enabled
- [ ] Type in text editor - no filtering occurs

### 9. Multi-Keyboard Test (if available)
If you have multiple keyboards connected:
- [ ] Console shows all detected keyboards
- [ ] All keyboards work with filtering enabled
- [ ] Statistics track all keyboards

### 10. Application Exit Test
- [ ] Close application normally
- [ ] Check console for "Cleaning up resources..." messages
- [ ] Verify no error messages
- [ ] Try typing - keyboard should work normally (not grabbed)
- [ ] Check no zombie processes: `ps aux | grep KeyboardUnchatter`

### 11. Signal Handling Test
Run application from terminal:
```bash
./KeyboardUnchatter.Linux
```
- [ ] Press Ctrl+C
- [ ] Application should gracefully shut down
- [ ] "Received interrupt signal" message appears
- [ ] Resources cleaned up properly

### 12. Error Conditions Test

**Test without permissions:**
```bash
# Remove from input group temporarily
sudo gpasswd -d $USER input
# Log out and log back in
./KeyboardUnchatter.Linux
```
- [ ] Application starts with warning messages
- [ ] Provides helpful instructions about permissions
- [ ] Doesn't crash

**Test without uinput:**
```bash
sudo rmmod uinput
./KeyboardUnchatter.Linux
```
- [ ] Application starts with uinput warning
- [ ] Reading works but injection may not
- [ ] Provides helpful message about loading uinput

## Expected Performance

- **Latency**: Key event processing should add < 1ms latency
- **CPU Usage**: Should be < 1% CPU when idle, < 5% when typing
- **Memory Usage**: Should be < 50MB resident memory
- **Chatter Detection Accuracy**: Should match Windows version (>95% accurate)

## Known Issues to Verify

1. **Wayland Compatibility**: Some Wayland compositors may restrict input device access
2. **Key Combinations**: Modifier keys (Ctrl, Alt, Shift) should work correctly
3. **Gaming**: Not recommended for gaming - may cause input lag
4. **Key Up Events**: Rarely, blocked key-up events may cause stuck keys (restart fixes)

## Logging and Debugging

Enable verbose logging by checking console output. Key messages to look for:
- "Found keyboard: [device name]"
- "Grabbed device: [device name]"
- "Key X pressed" / "Key X blocked"
- Typing speed updates

## Reporting Issues

When reporting issues, include:
1. Linux distribution and version
2. Desktop environment
3. .NET version (`dotnet --version`)
4. Console output
5. Contents of `~/.config/keyboard-unchatter/settings.json`
6. Output of: `ls -l /dev/input/event*`
7. Output of: `groups`

## Success Criteria

The Linux version passes testing if:
- [x] Builds successfully without errors
- [ ] Starts and runs on major Linux distributions
- [ ] GUI is functional and responsive
- [ ] Chatter detection works with same accuracy as Windows version
- [ ] Settings persist correctly between sessions
- [ ] No memory leaks (run `valgrind` for extended periods)
- [ ] Proper cleanup on exit (no zombie processes or grabbed devices)
- [ ] Permission checking provides helpful error messages
