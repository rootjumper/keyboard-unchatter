using System;
using System.IO;
using Newtonsoft.Json;

namespace KeyboardUnchatter.Linux.Configuration
{
    public class Settings
    {
        private static Settings? _instance;
        private static readonly object _lock = new object();
        private string _configPath;

        // Settings properties
        public double ChatterThreshold { get; set; } = 50;
        public bool OpenMinimized { get; set; } = false;
        public bool ActivateOnLaunch { get; set; } = false;
        public bool RunAtStartup { get; set; } = false;

        public static Settings Default
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new Settings();
                            _instance.Load();
                        }
                    }
                }
                return _instance;
            }
        }

        private Settings()
        {
            // Use XDG-compliant configuration directory
            string configDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") 
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            
            string appConfigDir = Path.Combine(configDir, "keyboard-unchatter");
            Directory.CreateDirectory(appConfigDir);
            
            _configPath = Path.Combine(appConfigDir, "settings.json");
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    var loadedSettings = JsonConvert.DeserializeObject<Settings>(json);
                    if (loadedSettings != null)
                    {
                        ChatterThreshold = loadedSettings.ChatterThreshold;
                        OpenMinimized = loadedSettings.OpenMinimized;
                        ActivateOnLaunch = loadedSettings.ActivateOnLaunch;
                        RunAtStartup = loadedSettings.RunAtStartup;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
        }

        public void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}
