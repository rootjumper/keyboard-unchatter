using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace KeyboardUnchatter
{
    public class InputHook : IDisposable
    {
        internal struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            int scanCode;
            public int flags;
            int time;
            int dwExtraInfo;
        }

        public enum KeyStatus
        {
            None = 0,
            Down = 1,
            Up   = 2
        }

        internal delegate IntPtr HookHandlerDelegate(int nCode, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;

        private const int WM_KEYUP = 0x0101;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYUP = 0x0105;
        private const int WM_SYSKEYDOWN = 0x0104;

        private HookHandlerDelegate _keyboardHookHandlerDelegate;

        private IntPtr _keyboardHookID = IntPtr.Zero;

        public Func<KeyPress,bool> OnHandleKey;

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

        // Optional: event for live updates
        public event Action<double> OnTypingMedianChanged;

        public InputHook()
        {
            _keyboardHookHandlerDelegate = new HookHandlerDelegate(KeyboardHookCallback);

            using (Process curProcess = Process.GetCurrentProcess())
            {
                using (ProcessModule curModule = curProcess.MainModule)
                {
                    _keyboardHookID = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardHookHandlerDelegate, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
                }
            }
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam)
        {
            bool allowContinue = true;

            KeyStatus keyStatus = KeyStatus.None;

            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP || wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                if(wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                {
                    keyStatus = KeyStatus.Up;
                }
                else if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    keyStatus = KeyStatus.Down;
                }

                allowContinue = HandleKey(new KeyPress(lParam.vkCode, keyStatus));
            }

            if (TypingSpeedEnabled)
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                var now = DateTime.Now;
                if (_lastKeyTime != DateTime.MinValue)
                {
                    var interval = (long)(now - _lastKeyTime).TotalMilliseconds;
                    _intervals.Add(interval);
                    if (_intervals.Count > MaxEntries)
                        _intervals.RemoveAt(0);

                    // Raise event if needed
                    OnTypingMedianChanged?.BeginInvoke(TypingMedianMs, null, null);
                }
                _lastKeyTime = now;
            }

            if(allowContinue)
            {
                return NativeMethods.CallNextHookEx(_keyboardHookID, nCode, wParam, ref lParam);
            }

            return (System.IntPtr)1;
        }

        public bool HandleKey(KeyPress e)
        {
            if (OnHandleKey != null)
            {
                return OnHandleKey(e);
            }

            return true;
        }

        public class KeyPress : System.EventArgs
        {
            private Keys _key;
            private int _keyCode;
            private KeyStatus _status;

            public Keys Key
            {
                get { return _key; }
            }

            public int KeyCode
            {
                get { return _keyCode; }
            }

            public KeyStatus Status
            {
                get { return _status; }
            }

            public KeyPress(int evtKeyCode, KeyStatus status)
            {
                _key = ((Keys)evtKeyCode);
                _keyCode = evtKeyCode;
                _status  = status;
            }
        }

        public void Dispose()
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHookID);
        }

        /// <summary>
        /// Rehooks the WH_KEYBOARD_LL hook. This is useful if the hook needs to be reset, for example after a system resume from sleep or hibernation.
        /// </summary>
        public void Rehook()
        {
            // Erst Hook entfernen, dann neu setzen
            if (_keyboardHookID != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_keyboardHookID);
                _keyboardHookID = IntPtr.Zero;
            }

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                _keyboardHookID = NativeMethods.SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardHookHandlerDelegate, NativeMethods.GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        public void ResetDiagnostics()
        {
            _intervals.Clear();
            _lastKeyTime = DateTime.MinValue;
            OnTypingMedianChanged?.Invoke(0);
        }

        #region Native methods

        [ComVisibleAttribute(false), System.Security.SuppressUnmanagedCodeSecurity()]
        internal class NativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook, HookHandlerDelegate lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, ref KBDLLHOOKSTRUCT lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
            public static extern short GetKeyState(int keyCode);

        }

        #endregion
    }
}
