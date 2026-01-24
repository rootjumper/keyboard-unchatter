using System;
using System.Collections.Generic;
using Gtk;
using KeyboardUnchatter.Linux.Configuration;

namespace KeyboardUnchatter.Linux
{
    public class MainWindow : Window
    {
        private KeyboardMonitor _keyboardMonitor;
        private LinuxInputHook _inputHook;
        
        // UI Controls
        private Button _activateButton;
        private Button _deactivateButton;
        private Button _resetButton;
        private SpinButton _thresholdInput;
        private CheckButton _minimizeCheckBox;
        private CheckButton _activateOnLaunchCheckBox;
        private CheckButton _runAtStartupCheckBox;
        private Label _statusLabel;
        private Label _typingSpeedLabel;
        private TreeView _statisticsTreeView;
        private ListStore _statisticsStore;
        
        // Statistics tracking
        private Dictionary<int, KeyStatistics> _keyStatistics = new Dictionary<int, KeyStatistics>();
        
        private class KeyStatistics
        {
            public int KeyCode { get; set; }
            public string KeyName { get; set; } = "";
            public int PressCount { get; set; }
            public int BlockCount { get; set; }
            public double FailureRate => PressCount > 0 ? (double)BlockCount / PressCount * 100 : 0;
        }

        public MainWindow(KeyboardMonitor monitor, LinuxInputHook inputHook) : base("Keyboard Unchatter Linux v1.0.0")
        {
            _keyboardMonitor = monitor;
            _inputHook = inputHook;
            
            SetDefaultSize(600, 500);
            SetPosition(WindowPosition.Center);
            
            // Subscribe to events
            _keyboardMonitor.OnKeyPress += OnKeyPress;
            _keyboardMonitor.OnKeyBlocked += OnKeyBlocked;
            _inputHook.OnTypingMedianChanged += UpdateTypingSpeed;
            
            DeleteEvent += OnDeleteEvent;
            
            BuildUI();
            
            LoadSettings();
        }

        private void BuildUI()
        {
            VBox mainVBox = new VBox(false, 10);
            mainVBox.BorderWidth = 10;
            
            // Status Panel
            Frame statusFrame = new Frame("Status");
            VBox statusBox = new VBox(false, 5);
            statusBox.BorderWidth = 5;
            
            _statusLabel = new Label("Status: Inactive");
            _statusLabel.Halign = Align.Start;
            statusBox.PackStart(_statusLabel, false, false, 0);
            
            _typingSpeedLabel = new Label("Typing speed: N/A");
            _typingSpeedLabel.Halign = Align.Start;
            statusBox.PackStart(_typingSpeedLabel, false, false, 0);
            
            HBox buttonBox = new HBox(false, 5);
            _activateButton = new Button("Activate");
            _activateButton.Clicked += OnActivateClicked;
            buttonBox.PackStart(_activateButton, false, false, 0);
            
            _deactivateButton = new Button("Deactivate");
            _deactivateButton.Clicked += OnDeactivateClicked;
            _deactivateButton.Sensitive = false;
            buttonBox.PackStart(_deactivateButton, false, false, 0);
            
            statusBox.PackStart(buttonBox, false, false, 0);
            statusFrame.Add(statusBox);
            mainVBox.PackStart(statusFrame, false, false, 0);
            
            // Settings Panel
            Frame settingsFrame = new Frame("Settings");
            VBox settingsBox = new VBox(false, 5);
            settingsBox.BorderWidth = 5;
            
            HBox thresholdBox = new HBox(false, 5);
            thresholdBox.PackStart(new Label("Chatter Threshold (ms):"), false, false, 0);
            _thresholdInput = new SpinButton(10, 200, 1);
            _thresholdInput.Value = 50;
            _thresholdInput.ValueChanged += OnThresholdChanged;
            thresholdBox.PackStart(_thresholdInput, false, false, 0);
            settingsBox.PackStart(thresholdBox, false, false, 0);
            
            _minimizeCheckBox = new CheckButton("Launch minimized");
            _minimizeCheckBox.Toggled += OnMinimizeCheckToggled;
            settingsBox.PackStart(_minimizeCheckBox, false, false, 0);
            
            _activateOnLaunchCheckBox = new CheckButton("Activate on launch");
            _activateOnLaunchCheckBox.Toggled += OnActivateOnLaunchToggled;
            settingsBox.PackStart(_activateOnLaunchCheckBox, false, false, 0);
            
            _runAtStartupCheckBox = new CheckButton("Run at startup");
            _runAtStartupCheckBox.Toggled += OnRunAtStartupToggled;
            settingsBox.PackStart(_runAtStartupCheckBox, false, false, 0);
            
            settingsFrame.Add(settingsBox);
            mainVBox.PackStart(settingsFrame, false, false, 0);
            
            // Statistics Panel
            Frame statisticsFrame = new Frame("Key Statistics");
            VBox statisticsBox = new VBox(false, 5);
            statisticsBox.BorderWidth = 5;
            
            // Create TreeView for statistics
            ScrolledWindow scrolledWindow = new ScrolledWindow();
            scrolledWindow.SetSizeRequest(-1, 200);
            
            _statisticsStore = new ListStore(typeof(string), typeof(int), typeof(int), typeof(string));
            _statisticsTreeView = new TreeView(_statisticsStore);
            
            _statisticsTreeView.AppendColumn("Key", new CellRendererText(), "text", 0);
            _statisticsTreeView.AppendColumn("Press Count", new CellRendererText(), "text", 1);
            _statisticsTreeView.AppendColumn("Chatter Count", new CellRendererText(), "text", 2);
            _statisticsTreeView.AppendColumn("Failure Rate", new CellRendererText(), "text", 3);
            
            scrolledWindow.Add(_statisticsTreeView);
            statisticsBox.PackStart(scrolledWindow, true, true, 0);
            
            _resetButton = new Button("Reset Statistics");
            _resetButton.Clicked += OnResetClicked;
            statisticsBox.PackStart(_resetButton, false, false, 0);
            
            statisticsFrame.Add(statisticsBox);
            mainVBox.PackStart(statisticsFrame, true, true, 0);
            
            Add(mainVBox);
        }

        private void LoadSettings()
        {
            var settings = Configuration.Settings.Default;
            _thresholdInput.Value = settings.ChatterThreshold;
            _minimizeCheckBox.Active = settings.OpenMinimized;
            _activateOnLaunchCheckBox.Active = settings.ActivateOnLaunch;
            _runAtStartupCheckBox.Active = settings.RunAtStartup;
            
            _keyboardMonitor.ChatterTimeMs = settings.ChatterThreshold;
            
            if (settings.ActivateOnLaunch)
            {
                ActivateMonitor();
            }
        }

        private void SaveSettings()
        {
            var settings = Configuration.Settings.Default;
            settings.ChatterThreshold = _thresholdInput.Value;
            settings.OpenMinimized = _minimizeCheckBox.Active;
            settings.ActivateOnLaunch = _activateOnLaunchCheckBox.Active;
            settings.RunAtStartup = _runAtStartupCheckBox.Active;
            settings.Save();
        }

        private void OnActivateClicked(object? sender, EventArgs e)
        {
            ActivateMonitor();
        }

        private void OnDeactivateClicked(object? sender, EventArgs e)
        {
            DeactivateMonitor();
        }

        private void ActivateMonitor()
        {
            _keyboardMonitor.Activate();
            _activateButton.Sensitive = false;
            _deactivateButton.Sensitive = true;
            _statusLabel.Text = "Status: Active";
            _inputHook.TypingSpeedEnabled = true;
        }

        private void DeactivateMonitor()
        {
            _keyboardMonitor.Deactivate();
            _activateButton.Sensitive = true;
            _deactivateButton.Sensitive = false;
            _statusLabel.Text = "Status: Inactive";
            _inputHook.TypingSpeedEnabled = false;
        }

        private void OnResetClicked(object? sender, EventArgs e)
        {
            _keyStatistics.Clear();
            _statisticsStore.Clear();
            _keyboardMonitor.ResetStatistics();
            _inputHook.ResetDiagnostics();
            _typingSpeedLabel.Text = "Typing speed: N/A";
        }

        private void OnThresholdChanged(object? sender, EventArgs e)
        {
            _keyboardMonitor.ChatterTimeMs = _thresholdInput.Value;
            SaveSettings();
        }

        private void OnMinimizeCheckToggled(object? sender, EventArgs e)
        {
            SaveSettings();
        }

        private void OnActivateOnLaunchToggled(object? sender, EventArgs e)
        {
            SaveSettings();
        }

        private void OnRunAtStartupToggled(object? sender, EventArgs e)
        {
            SaveSettings();
            UpdateAutostart(_runAtStartupCheckBox.Active);
        }

        private void OnKeyPress(int keyCode)
        {
            Application.Invoke((s, e) =>
            {
                if (!_keyStatistics.ContainsKey(keyCode))
                {
                    _keyStatistics[keyCode] = new KeyStatistics
                    {
                        KeyCode = keyCode,
                        KeyName = GetKeyName(keyCode)
                    };
                }
                
                _keyStatistics[keyCode].PressCount++;
                UpdateStatisticsView();
            });
        }

        private void OnKeyBlocked(int keyCode)
        {
            Application.Invoke((s, e) =>
            {
                if (!_keyStatistics.ContainsKey(keyCode))
                {
                    _keyStatistics[keyCode] = new KeyStatistics
                    {
                        KeyCode = keyCode,
                        KeyName = GetKeyName(keyCode)
                    };
                }
                
                _keyStatistics[keyCode].BlockCount++;
                UpdateStatisticsView();
            });
        }

        private void UpdateStatisticsView()
        {
            _statisticsStore.Clear();
            
            foreach (var kvp in _keyStatistics)
            {
                var stat = kvp.Value;
                _statisticsStore.AppendValues(
                    stat.KeyName,
                    stat.PressCount,
                    stat.BlockCount,
                    $"{stat.FailureRate:F2}%"
                );
            }
        }

        private void UpdateTypingSpeed(double medianMs)
        {
            Application.Invoke((s, e) =>
            {
                if (medianMs > 0)
                {
                    _typingSpeedLabel.Text = $"Typing speed: {medianMs:F0} ms (median interval)";
                }
                else
                {
                    _typingSpeedLabel.Text = "Typing speed: N/A";
                }
            });
        }

        private string GetKeyName(int keyCode)
        {
            // Basic key name mapping for common keys
            var keyNames = new Dictionary<int, string>
            {
                { 1, "ESC" }, { 2, "1" }, { 3, "2" }, { 4, "3" }, { 5, "4" },
                { 6, "5" }, { 7, "6" }, { 8, "7" }, { 9, "8" }, { 10, "9" },
                { 11, "0" }, { 16, "Q" }, { 17, "W" }, { 18, "E" }, { 19, "R" },
                { 20, "T" }, { 21, "Y" }, { 22, "U" }, { 23, "I" }, { 24, "O" },
                { 25, "P" }, { 30, "A" }, { 31, "S" }, { 32, "D" }, { 33, "F" },
                { 34, "G" }, { 35, "H" }, { 36, "J" }, { 37, "K" }, { 38, "L" },
                { 44, "Z" }, { 45, "X" }, { 46, "C" }, { 47, "V" }, { 48, "B" },
                { 49, "N" }, { 50, "M" }, { 57, "SPACE" }, { 28, "ENTER" },
            };
            
            return keyNames.ContainsKey(keyCode) ? keyNames[keyCode] : $"Key{keyCode}";
        }

        private void UpdateAutostart(bool enable)
        {
            try
            {
                string autostartDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config", "autostart"
                );
                Directory.CreateDirectory(autostartDir);
                
                string desktopFilePath = System.IO.Path.Combine(autostartDir, "keyboard-unchatter.desktop");
                
                if (enable)
                {
                    // Try to get the actual executable path
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName 
                                     ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                    
                    // If it's still a .dll path, try to use the standard installation location
                    if (exePath.EndsWith(".dll"))
                    {
                        exePath = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".local", "bin", "KeyboardUnchatter.Linux"
                        );
                    }
                    
                    string desktopEntry = $@"[Desktop Entry]
Type=Application
Name=Keyboard Unchatter
Comment=Fix keyboard chattering
Exec={exePath}
Icon=input-keyboard
Terminal=false
Categories=Utility;
StartupNotify=false
X-GNOME-Autostart-enabled=true
";
                    File.WriteAllText(desktopFilePath, desktopEntry);
                }
                else
                {
                    if (File.Exists(desktopFilePath))
                    {
                        File.Delete(desktopFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating autostart: {ex.Message}");
            }
        }

        private void OnDeleteEvent(object sender, DeleteEventArgs args)
        {
            Application.Quit();
            args.RetVal = true;
        }
    }
}
