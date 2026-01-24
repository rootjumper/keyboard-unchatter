using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace KeyboardUnchatter.Linux
{
    public class LinuxInputHook : IDisposable
    {
        private const string INPUT_DEVICES_PATH = "/dev/input";
        private const int KEY_MAX = 767;

        private List<InputDevice> _devices = new List<InputDevice>();
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _monitorTask;
        private int _uinputFd = -1;
        
        public Func<int, KeyStatus, bool>? OnHandleKey;

        // Typing speed tracking
        private List<long> _intervals = new List<long>();
        private DateTime _lastKeyTime = DateTime.MinValue;
        private const int MaxEntries = 50;

        public bool TypingSpeedEnabled { get; set; } = false;

        public double TypingMedianMs
        {
            get
            {
                if (_intervals.Count == 0) return 0;
                var sorted = _intervals.OrderBy(x => x).ToList();
                int count = sorted.Count;
                if (count % 2 == 1)
                    return sorted[count / 2];
                else
                    return (sorted[(count / 2) - 1] + sorted[count / 2]) / 2.0;
            }
        }

        public event Action<double>? OnTypingMedianChanged;

        private class InputDevice
        {
            public string Path { get; set; } = "";
            public string Name { get; set; } = "";
            public int Fd { get; set; } = -1;
            public bool IsKeyboard { get; set; } = false;
        }

        public LinuxInputHook()
        {
            InitializeUinput();
            DiscoverKeyboards();
        }

        private void InitializeUinput()
        {
            try
            {
                _uinputFd = NativeMethods.open("/dev/uinput", NativeMethods.O_WRONLY | NativeMethods.O_NONBLOCK);
                if (_uinputFd < 0)
                {
                    Console.WriteLine("Warning: Could not open /dev/uinput. Event injection will not work.");
                    Console.WriteLine("You may need to run: sudo modprobe uinput");
                    return;
                }

                // Setup uinput device
                SetupUinputDevice();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing uinput: {ex.Message}");
            }
        }

        private unsafe void SetupUinputDevice()
        {
            if (_uinputFd < 0) return;

            // Enable key events
            NativeMethods.ioctl(_uinputFd, NativeMethods.UI_SET_EVBIT, NativeMethods.EV_KEY);
            NativeMethods.ioctl(_uinputFd, NativeMethods.UI_SET_EVBIT, NativeMethods.EV_SYN);

            // Enable all key codes
            for (int i = 0; i < KEY_MAX; i++)
            {
                NativeMethods.ioctl(_uinputFd, NativeMethods.UI_SET_KEYBIT, i);
            }

            // Create uinput device
            var usetup = new NativeMethods.uinput_setup();
            usetup.id.bustype = NativeMethods.BUS_USB;
            usetup.id.vendor = 0x1234;
            usetup.id.product = 0x5678;
            
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes("Keyboard Unchatter Virtual Device");
            for (int i = 0; i < Math.Min(nameBytes.Length, 80); i++)
            {
                usetup.name[i] = nameBytes[i];
            }

            NativeMethods.ioctl(_uinputFd, NativeMethods.UI_DEV_SETUP, new IntPtr(&usetup));
            NativeMethods.ioctl(_uinputFd, NativeMethods.UI_DEV_CREATE, IntPtr.Zero);
        }

        private void DiscoverKeyboards()
        {
            try
            {
                if (!Directory.Exists(INPUT_DEVICES_PATH))
                {
                    throw new Exception($"Input devices path {INPUT_DEVICES_PATH} does not exist");
                }

                var eventFiles = Directory.GetFiles(INPUT_DEVICES_PATH, "event*");
                
                foreach (var eventFile in eventFiles)
                {
                    try
                    {
                        if (IsKeyboardDevice(eventFile))
                        {
                            var device = new InputDevice
                            {
                                Path = eventFile,
                                Name = GetDeviceName(eventFile),
                                IsKeyboard = true
                            };
                            
                            _devices.Add(device);
                            Console.WriteLine($"Found keyboard: {device.Name} at {device.Path}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error checking device {eventFile}: {ex.Message}");
                    }
                }

                if (_devices.Count == 0)
                {
                    Console.WriteLine("Warning: No keyboard devices found!");
                    Console.WriteLine("You may need to:");
                    Console.WriteLine("1. Add your user to the 'input' group: sudo usermod -a -G input $USER");
                    Console.WriteLine("2. Log out and log back in");
                    Console.WriteLine("3. Or run this application with elevated privileges");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error discovering keyboards: {ex.Message}");
            }
        }

        private bool IsKeyboardDevice(string devicePath)
        {
            int fd = -1;
            try
            {
                fd = NativeMethods.open(devicePath, NativeMethods.O_RDONLY);
                if (fd < 0)
                {
                    return false;
                }

                // Check if device has KEY_A capability (typical for keyboards)
                byte[] keyBits = new byte[KEY_MAX / 8 + 1];
                unsafe
                {
                    fixed (byte* ptr = keyBits)
                    {
                        if (NativeMethods.ioctl(fd, NativeMethods.EVIOCGBIT(NativeMethods.EV_KEY, keyBits.Length), new IntPtr(ptr)) < 0)
                        {
                            return false;
                        }
                    }
                }

                // Check for KEY_A (30) - if a device has this, it's likely a keyboard
                int keyA = 30;
                return (keyBits[keyA / 8] & (1 << (keyA % 8))) != 0;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (fd >= 0)
                {
                    NativeMethods.close(fd);
                }
            }
        }

        private unsafe string GetDeviceName(string devicePath)
        {
            int fd = -1;
            try
            {
                fd = NativeMethods.open(devicePath, NativeMethods.O_RDONLY);
                if (fd < 0)
                {
                    return "Unknown";
                }

                byte[] nameBuffer = new byte[256];
                fixed (byte* ptr = nameBuffer)
                {
                    if (NativeMethods.ioctl(fd, NativeMethods.EVIOCGNAME(nameBuffer.Length), new IntPtr(ptr)) < 0)
                    {
                        return "Unknown";
                    }
                }

                int length = Array.IndexOf(nameBuffer, (byte)0);
                if (length < 0) length = nameBuffer.Length;
                
                return System.Text.Encoding.UTF8.GetString(nameBuffer, 0, length);
            }
            catch
            {
                return "Unknown";
            }
            finally
            {
                if (fd >= 0)
                {
                    NativeMethods.close(fd);
                }
            }
        }

        public void Start()
        {
            if (_monitorTask != null)
            {
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorDevices(_cancellationTokenSource.Token));
        }

        private async Task MonitorDevices(CancellationToken cancellationToken)
        {
            // Open all keyboard devices
            foreach (var device in _devices)
            {
                try
                {
                    device.Fd = NativeMethods.open(device.Path, NativeMethods.O_RDONLY | NativeMethods.O_NONBLOCK);
                    if (device.Fd < 0)
                    {
                        Console.WriteLine($"Failed to open {device.Path}: Permission denied");
                        continue;
                    }

                    // Grab the device to intercept events
                    NativeMethods.ioctl(device.Fd, NativeMethods.EVIOCGRAB, 1);
                    Console.WriteLine($"Grabbed device: {device.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error opening device {device.Path}: {ex.Message}");
                }
            }

            // Monitor all devices
            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var device in _devices.Where(d => d.Fd >= 0))
                {
                    try
                    {
                        ProcessDeviceEvents(device);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing events from {device.Name}: {ex.Message}");
                    }
                }

                await Task.Delay(5, cancellationToken);
            }

            // Release devices
            foreach (var device in _devices.Where(d => d.Fd >= 0))
            {
                try
                {
                    NativeMethods.ioctl(device.Fd, NativeMethods.EVIOCGRAB, 0);
                    NativeMethods.close(device.Fd);
                    device.Fd = -1;
                }
                catch { }
            }
        }

        private unsafe void ProcessDeviceEvents(InputDevice device)
        {
            NativeMethods.input_event evt;
            int evtSize = sizeof(NativeMethods.input_event);

            while (true)
            {
                int result = NativeMethods.read(device.Fd, &evt, evtSize);
                if (result != evtSize)
                {
                    break;
                }

                // Only process key events
                if (evt.type == NativeMethods.EV_KEY)
                {
                    KeyStatus status = evt.value == 1 ? KeyStatus.Down : 
                                      evt.value == 0 ? KeyStatus.Up : 
                                      KeyStatus.None;

                    if (status != KeyStatus.None)
                    {
                        bool allow = HandleKeyEvent((int)evt.code, status);
                        
                        // If allowed, inject the event into uinput
                        if (allow)
                        {
                            InjectEvent(evt);
                        }
                    }
                }
                else if (evt.type == NativeMethods.EV_SYN)
                {
                    // Always pass through synchronization events
                    InjectEvent(evt);
                }
            }
        }

        private bool HandleKeyEvent(int keyCode, KeyStatus status)
        {
            // Track typing speed for key down events
            if (TypingSpeedEnabled && status == KeyStatus.Down)
            {
                var now = DateTime.Now;
                if (_lastKeyTime != DateTime.MinValue)
                {
                    var interval = (long)(now - _lastKeyTime).TotalMilliseconds;
                    _intervals.Add(interval);
                    if (_intervals.Count > MaxEntries)
                        _intervals.RemoveAt(0);

                    Task.Run(() => OnTypingMedianChanged?.Invoke(TypingMedianMs));
                }
                _lastKeyTime = now;
            }

            // Call the handler
            if (OnHandleKey != null)
            {
                return OnHandleKey(keyCode, status);
            }

            return true;
        }

        private unsafe void InjectEvent(NativeMethods.input_event evt)
        {
            if (_uinputFd < 0) return;

            NativeMethods.write(_uinputFd, &evt, sizeof(NativeMethods.input_event));
        }

        public void ResetDiagnostics()
        {
            _intervals.Clear();
            _lastKeyTime = DateTime.MinValue;
            OnTypingMedianChanged?.Invoke(0);
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _monitorTask?.Wait(TimeSpan.FromSeconds(2));

            foreach (var device in _devices.Where(d => d.Fd >= 0))
            {
                try
                {
                    NativeMethods.ioctl(device.Fd, NativeMethods.EVIOCGRAB, 0);
                    NativeMethods.close(device.Fd);
                }
                catch { }
            }

            if (_uinputFd >= 0)
            {
                NativeMethods.ioctl(_uinputFd, NativeMethods.UI_DEV_DESTROY, IntPtr.Zero);
                NativeMethods.close(_uinputFd);
                _uinputFd = -1;
            }

            _devices.Clear();
        }

        #region Native Methods and Structures

        private static class NativeMethods
        {
            public const int O_RDONLY = 0x0000;
            public const int O_WRONLY = 0x0001;
            public const int O_NONBLOCK = 0x0800;

            public const ushort EV_SYN = 0x00;
            public const ushort EV_KEY = 0x01;

            public const int BUS_USB = 0x03;

            public const uint UI_DEV_CREATE = 0x5501;
            public const uint UI_DEV_DESTROY = 0x5502;
            public const uint UI_DEV_SETUP = 0x405c5503;
            public const uint UI_SET_EVBIT = 0x40045564;
            public const uint UI_SET_KEYBIT = 0x40045565;

            public const int EVIOCGRAB = 0x40044590;

            public static uint EVIOCGBIT(int ev, int len) => 0x80000000 | (((uint)len & 0x1FFF) << 16) | (0x45 << 8) | (0x20 + (uint)ev);
            public static uint EVIOCGNAME(int len) => 0x80000000 | (((uint)len & 0x1FFF) << 16) | (0x45 << 8) | 0x06;

            [DllImport("libc", SetLastError = true)]
            public static extern int open([MarshalAs(UnmanagedType.LPStr)] string pathname, int flags);

            [DllImport("libc", SetLastError = true)]
            public static extern int close(int fd);

            [DllImport("libc", SetLastError = true)]
            public static extern unsafe int read(int fd, void* buf, int count);

            [DllImport("libc", SetLastError = true)]
            public static extern unsafe int write(int fd, void* buf, int count);

            [DllImport("libc", SetLastError = true)]
            public static extern int ioctl(int fd, uint request, int value);

            [DllImport("libc", SetLastError = true)]
            public static extern int ioctl(int fd, uint request, IntPtr argp);

            [StructLayout(LayoutKind.Sequential)]
            public struct input_event
            {
                public long tv_sec;
                public long tv_usec;
                public ushort type;
                public ushort code;
                public int value;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct input_id
            {
                public ushort bustype;
                public ushort vendor;
                public ushort product;
                public ushort version;
            }

            [StructLayout(LayoutKind.Sequential)]
            public unsafe struct uinput_setup
            {
                public input_id id;
                public fixed byte name[80];
                public uint ff_effects_max;
            }
        }

        #endregion
    }
}
