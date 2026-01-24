# Keyboard Unchatter
Application to fix the keyboard chattering of mechanical switches

## Overview

This application fixes repeated keys in damaged mechanical switches by filtering all repeated keystrokes that occur within a certain timespan. If two or more keypresses are detected inside that time, only the first keypress will be sent to other applications.

## Platform Support

- **Windows**: Full-featured Windows application with Windows Forms GUI ([KeyboardUnchatter/](KeyboardUnchatter/))
- **Linux**: Native Linux version with GTK+ GUI ([KeyboardUnchatter.Linux/](KeyboardUnchatter.Linux/)) - See [Linux README](KeyboardUnchatter.Linux/README.md) for installation and usage instructions

## New Features

- **Diagnostics:**  
  Analyze your typing speed and keypress intervals to help determine the optimal chatter threshold. The diagnostics view provides real-time feedback and statistics.

- **Reset Functionality:**  
  A reset button is available to clear all diagnostics, statistics, and keypress data, allowing you to start fresh measurements at any time.

- **System Startup Option:**  
  You can enable or disable automatic application launch at Windows startup directly from the settings.
  
- **Automatic Rehook after Resume:**  
  The application automatically restores the keyboard hook after your computer resumes from sleep or hibernate, ensuring continued protection against key chatter without needing to restart the app.

# Releases

[Version 1.0.1](https://github.com/ZoserLock/keyboard-unchatter/releases/tag/v1.0.1)
[Version 1.1.0](https://github.com/rootjumper/keyboard-unchatter/releases/tag/v1.1.0)

# Known Issues

While this tool works well while typing, the method used is not fully reliable. In some cases, the **Key Up** event is not registered because it was blocked by the app, which can result in bad output if the running application is, for example, a game.

There is no way to know if the **Key Up** event was triggered by the user or by keyboard chattering, so the problem is not fully fixable using this method.

I don't recommend using this tool while playing games. In future releases, an option to disable the application if a selected process is running will be added, since the chattering problem does not affect games that much.

# To Do List

* Add option to disable all statistics.
* Add a list of applications to disable the action of this tool. (ex: Games)

# Screenshots

![Example](https://github.com/rootjumper/keyboard-unchatter/raw/master/Images/example.png)
