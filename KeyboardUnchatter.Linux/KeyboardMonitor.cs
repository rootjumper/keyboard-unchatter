using System;
using System.Collections.Generic;

namespace KeyboardUnchatter.Linux
{
    public class KeyboardMonitor
    {
        private bool _active = false;
        private KeyStatusList _keyStatusList = new KeyStatusList();
        private double _chatterTimeMs = 50; // Default 50ms as per requirements

        public event Action<int>? OnKeyPress;
        public event Action<int>? OnKeyBlocked;

        #region Get/Set

        public bool Active
        {
            get => _active;
            set => _active = value;
        }

        public double ChatterTimeMs
        {
            get => _chatterTimeMs;
            set => _chatterTimeMs = value;
        }

        #endregion

        public KeyboardMonitor()
        {
        }

        public void Activate()
        {
            if (!_active)
            {
                _keyStatusList.Clear();
                _active = true;
            }
        }

        public void Deactivate()
        {
            _active = false;
        }

        public bool HandleKey(int keyCode, KeyStatus status)
        {
            if (!_active)
            {
                return true;
            }

            var key = _keyStatusList.GetKey(keyCode);

            if (status == KeyStatus.Down)
            {
                var lastPressStatus = key.LastPressStatus;
                key.LastPressStatus = KeyStatusList.PressStatus.Down;

                if (lastPressStatus == KeyStatusList.PressStatus.Up && key.IsBlocked)
                {
                    Console.WriteLine($"Key {key.KeyCode} is blocked. Discarding");
                    return false;
                }

                double timeSpan = key.GetLastPressTimeSpan();

                if (timeSpan < _chatterTimeMs)
                {
                    Console.WriteLine($"Key {key.KeyCode} timeSpan: {timeSpan}ms is below limit. Blocking");
                    key.Block();
                    RegisterChatterPress(keyCode);
                    return false;
                }
            }

            if (status == KeyStatus.Up)
            {
                var lastPressStatus = key.LastPressStatus;
                key.LastPressStatus = KeyStatusList.PressStatus.Up;

                bool keyWasBlocked = key.IsBlocked;
                key.Press();

                if (keyWasBlocked && key.GetBlockTimeSpan() < _chatterTimeMs)
                {
                    Console.WriteLine($"Key {key.KeyCode} was blocked");
                    return false;
                }
                else
                {
                    Console.WriteLine($"Key {key.KeyCode} pressed");
                    RegisterPress(keyCode);
                }
            }

            return true;
        }

        private void RegisterPress(int keyCode)
        {
            OnKeyPress?.Invoke(keyCode);
        }

        private void RegisterChatterPress(int keyCode)
        {
            OnKeyPress?.Invoke(keyCode);
            OnKeyBlocked?.Invoke(keyCode);
        }

        public void ResetStatistics()
        {
            _keyStatusList.Clear();
        }
    }

    public enum KeyStatus
    {
        None = 0,
        Down = 1,
        Up = 2
    }
}
