using System;
using System.Runtime.InteropServices;
using System.Threading;
using Gtk;
using KeyboardUnchatter.Linux.Configuration;

namespace KeyboardUnchatter.Linux
{
    public class Program
    {
        private static LinuxInputHook? _inputHook;
        private static KeyboardMonitor? _keyboardMonitor;
        private static MainWindow? _mainWindow;
        private static bool _running = true;

        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Keyboard Unchatter Linux v1.0.0");
                Console.WriteLine("================================");
                
                // Check if we have permission to access input devices
                CheckPermissions();
                
                // Setup signal handlers for graceful shutdown
                SetupSignalHandlers();
                
                // Initialize GTK
                Application.Init();
                
                // Create input hook and keyboard monitor
                _inputHook = new LinuxInputHook();
                _keyboardMonitor = new KeyboardMonitor();
                
                // Wire up the input hook to keyboard monitor
                _inputHook.OnHandleKey = (keyCode, status) =>
                {
                    return _keyboardMonitor.HandleKey(keyCode, status);
                };
                
                // Start monitoring input devices
                _inputHook.Start();
                
                // Create and show main window
                _mainWindow = new MainWindow(_keyboardMonitor, _inputHook);
                
                // Check if we should start minimized
                if (!Configuration.Settings.Default.OpenMinimized)
                {
                    _mainWindow.ShowAll();
                }
                
                // Run GTK main loop
                Application.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
            finally
            {
                Cleanup();
            }
        }

        private static void CheckPermissions()
        {
            // Check if we can access /dev/input
            if (!Directory.Exists("/dev/input"))
            {
                Console.WriteLine("ERROR: /dev/input directory not found!");
                Environment.Exit(1);
            }

            // Check if we can read event files
            var eventFiles = Directory.GetFiles("/dev/input", "event*");
            if (eventFiles.Length == 0)
            {
                Console.WriteLine("WARNING: No input event devices found in /dev/input");
            }

            bool canAccess = false;
            foreach (var file in eventFiles)
            {
                try
                {
                    using (var fs = File.OpenRead(file))
                    {
                        canAccess = true;
                        break;
                    }
                }
                catch
                {
                    continue;
                }
            }

            if (!canAccess && eventFiles.Length > 0)
            {
                Console.WriteLine("WARNING: Cannot access input devices!");
                Console.WriteLine("You need to either:");
                Console.WriteLine("  1. Add your user to the 'input' group:");
                Console.WriteLine("     sudo usermod -a -G input $USER");
                Console.WriteLine("     Then log out and log back in");
                Console.WriteLine("  2. Or run this application with elevated privileges:");
                Console.WriteLine("     sudo dotnet run");
                Console.WriteLine();
                Console.WriteLine("The application will continue, but keyboard interception may not work.");
            }
            else if (canAccess)
            {
                Console.WriteLine("✓ Input device access: OK");
            }
        }

        private static void SetupSignalHandlers()
        {
            // Handle Ctrl+C and other termination signals
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.WriteLine("\nReceived interrupt signal. Shutting down gracefully...");
                eventArgs.Cancel = true;
                _running = false;
                Application.Quit();
            };

            // On Unix-like systems, also handle SIGTERM
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
                {
                    Console.WriteLine("Process exit requested. Cleaning up...");
                    Cleanup();
                };
            }
        }

        private static void Cleanup()
        {
            Console.WriteLine("Cleaning up resources...");
            
            try
            {
                _inputHook?.Dispose();
                Console.WriteLine("✓ Input hook disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing input hook: {ex.Message}");
            }

            try
            {
                Configuration.Settings.Default.Save();
                Console.WriteLine("✓ Settings saved");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }

            Console.WriteLine("Shutdown complete.");
        }
    }
}
