using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HBYSBridge.Models;

namespace HBYSBridge.Services
{
    public class UniversalSnifferService
    {
        // 1. GAMEPAD & XBOX MOTORU
        private readonly GamepadService _gamepadService = new();

        // 2. SERİ PORT / COM PORT MOTORU
        private readonly ConcurrentDictionary<string, SerialPort> _activeSerialPorts = new();
        private CancellationTokenSource? _scanCts;

        // 3. GLOBAL KLAVYE & FARE MOTORU
        private IntPtr _keyboardHookId = IntPtr.Zero;
        private IntPtr _mouseHookId = IntPtr.Zero;
        private LowLevelProc? _keyboardProc;
        private LowLevelProc? _mouseProc;

        private bool _isSniffing = false;

        public event Action<CapturedSignal>? SignalCaptured;
        public event Action<string>? LogMessage;
        public event Action<string, bool>? ControllerStatusChanged;

        public void Start()
        {
            if (_isSniffing) return;
            _isSniffing = true;
            _scanCts = new CancellationTokenSource();

            // A) Gamepad
            _gamepadService.ButtonStateChanged += OnGamepadButton;
            _gamepadService.ControllerConnected += (name, connected) => ControllerStatusChanged?.Invoke(name, connected);
            _gamepadService.Start();

            // B) Seri Port / COM Port
            Task.Run(() => SerialPortAutoScanLoop(_scanCts.Token));

            // C) Global Klavye & Fare
            InstallGlobalHooks();

            LogMessage?.Invoke("[Donanım Servisi] USB ve COM Port dinleyicileri aktif (Gamepad, XInput, Seri Port).");
        }

        public void Stop()
        {
            _isSniffing = false;
            _scanCts?.Cancel();
            _gamepadService.Stop();

            UninstallGlobalHooks();

            foreach (var sp in _activeSerialPorts.Values)
            {
                try
                {
                    if (sp.IsOpen) sp.Close();
                    sp.Dispose();
                }
                catch { }
            }
            _activeSerialPorts.Clear();
        }

        // --- SANAL PEDAL VE XBOX SİMÜLATÖRÜ ---
        public void SimulateComPedalPress(string portName = "COM3")
        {
            Task.Run(async () =>
            {
                // 1. Basıldı Sinyali
                var pressSignal = new CapturedSignal
                {
                    DeviceType = "Seri Port (COM)",
                    DeviceName = $"{portName} Hualin HL-300 Ayak Pedalı (Simülasyon)",
                    SignalId = $"{portName} - CTS (Pedal Kontak)",
                    RawData = "KONTAK KAPALI (PEDALA BASILDI)",
                    IsPressed = true,
                    Timestamp = DateTime.Now
                };
                SignalCaptured?.Invoke(pressSignal);

                // 2. 500 ms sonra Bırakıldı Sinyali
                await Task.Delay(500);

                var releaseSignal = new CapturedSignal
                {
                    DeviceType = "Seri Port (COM)",
                    DeviceName = $"{portName} Hualin HL-300 Ayak Pedalı (Simülasyon)",
                    SignalId = $"{portName} - CTS (Pedal Kontak)",
                    RawData = "KONTAK AÇIK (PEDAL BIRAKILDI)",
                    IsPressed = false,
                    Timestamp = DateTime.Now
                };
                SignalCaptured?.Invoke(releaseSignal);
            });
        }

        public void SimulateXboxButtonPress(string buttonName = "Xbox - A Tuşu")
        {
            Task.Run(async () =>
            {
                var pressSignal = new CapturedSignal
                {
                    DeviceType = "Oyun Kolu (USB)",
                    DeviceName = "Xbox Controller (XInput)",
                    SignalId = buttonName,
                    RawData = "BUTON BASILDI",
                    IsPressed = true,
                    Timestamp = DateTime.Now
                };
                SignalCaptured?.Invoke(pressSignal);

                await Task.Delay(400);

                var releaseSignal = new CapturedSignal
                {
                    DeviceType = "Oyun Kolu (USB)",
                    DeviceName = "Xbox Controller (XInput)",
                    SignalId = buttonName,
                    RawData = "BUTON BIRAKILDI",
                    IsPressed = false,
                    Timestamp = DateTime.Now
                };
                SignalCaptured?.Invoke(releaseSignal);
            });
        }

        private void OnGamepadButton(string buttonName, bool isPressed)
        {
            var signal = new CapturedSignal
            {
                DeviceType = "Oyun Kolu (USB)",
                DeviceName = buttonName.StartsWith("Xbox") ? "Xbox Controller (XInput)" : "Snopy / USB Joystick (DirectInput)",
                SignalId = buttonName,
                RawData = isPressed ? "BUTON BASILDI" : "BUTON BIRAKILDI",
                IsPressed = isPressed,
                Timestamp = DateTime.Now
            };

            SignalCaptured?.Invoke(signal);
        }

        private async Task SerialPortAutoScanLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var availablePorts = SerialPort.GetPortNames();

                    foreach (var port in availablePorts)
                    {
                        if (!_activeSerialPorts.ContainsKey(port))
                        {
                            TryOpenSerialPort(port);
                        }
                    }
                }
                catch { }

                try
                {
                    await Task.Delay(3000, token);
                }
                catch { break; }
            }
        }

        private void TryOpenSerialPort(string portName)
        {
            try
            {
                var sp = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
                {
                    DtrEnable = true,
                    RtsEnable = true,
                    ReadTimeout = 500
                };

                sp.PinChanged += (s, e) =>
                {
                    bool pressed = false;
                    string pinName = e.EventType.ToString();

                    if (e.EventType == SerialPinChange.CtsChanged)
                    {
                        pressed = sp.CtsHolding;
                        pinName = "CTS (Pedal Kontak)";
                    }
                    else if (e.EventType == SerialPinChange.DsrChanged)
                    {
                        pressed = sp.DsrHolding;
                        pinName = "DSR (Pedal Kontak)";
                    }

                    var signal = new CapturedSignal
                    {
                        DeviceType = "Seri Port (COM)",
                        DeviceName = $"{portName} Ayak Pedalı",
                        SignalId = $"{portName} - {pinName}",
                        RawData = pressed ? "KONTAK KAPALI (BASILDI)" : "KONTAK AÇIK (BIRAKILDI)",
                        IsPressed = pressed,
                        Timestamp = DateTime.Now
                    };

                    SignalCaptured?.Invoke(signal);
                };

                sp.DataReceived += (s, e) =>
                {
                    try
                    {
                        if (sp.IsOpen && sp.BytesToRead > 0)
                        {
                            byte[] buffer = new byte[sp.BytesToRead];
                            int read = sp.Read(buffer, 0, buffer.Length);
                            string hex = BitConverter.ToString(buffer, 0, read);

                            var signal = new CapturedSignal
                            {
                                DeviceType = "Seri Port (COM)",
                                DeviceName = $"{portName} Seri Aygıt",
                                SignalId = $"{portName} - Veri Akışı",
                                RawData = $"Gelen Byte: {hex}",
                                IsPressed = true,
                                Timestamp = DateTime.Now
                            };

                            SignalCaptured?.Invoke(signal);
                        }
                    }
                    catch { }
                };

                sp.Open();
                if (_activeSerialPorts.TryAdd(portName, sp))
                {
                    LogMessage?.Invoke($"[Seri Port Bulundu] {portName} otomatik dinlemeye alındı.");
                }
            }
            catch { }
        }

        // --- GLOBAL HOOKS ---
        private delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_MBUTTONDOWN = 0x0207;

        private void InstallGlobalHooks()
        {
            try
            {
                using var curProcess = Process.GetCurrentProcess();
                using var curModule = curProcess.MainModule;
                var modHandle = GetModuleHandle(curModule?.ModuleName);

                _keyboardProc = HookKeyboardCallback;
                _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, modHandle, 0);

                _mouseProc = HookMouseCallback;
                _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, modHandle, 0);
            }
            catch { }
        }

        private void UninstallGlobalHooks()
        {
            if (_keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = IntPtr.Zero;
            }
            if (_mouseHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = IntPtr.Zero;
            }
        }

        private IntPtr HookKeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool isDown = (wParam == (IntPtr)WM_KEYDOWN);
                bool isUp = (wParam == (IntPtr)WM_KEYUP);

                if (isDown || isUp)
                {
                    string keyName = ((System.Windows.Input.Key)System.Windows.Input.KeyInterop.KeyFromVirtualKey(vkCode)).ToString();

                    if (keyName.StartsWith("F") || keyName == "Space" || keyName.Contains("NumPad"))
                    {
                        var signal = new CapturedSignal
                        {
                            DeviceType = "USB HID (Klavye Modu)",
                            DeviceName = "USB Klavye / HID Pedal",
                            SignalId = $"Klavye - {keyName}",
                            RawData = isDown ? "TUŞ BASILDI" : "TUŞ BIRAKILDI",
                            IsPressed = isDown,
                            Timestamp = DateTime.Now
                        };

                        SignalCaptured?.Invoke(signal);
                    }
                }
            }
            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        private IntPtr HookMouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                if (wParam == (IntPtr)WM_MBUTTONDOWN)
                {
                    var signal = new CapturedSignal
                    {
                        DeviceType = "USB HID (Fare Modu)",
                        DeviceName = "Cerrahi / Medikal Ayak Pedalı",
                        SignalId = "Fare - Orta Tık (Scroll Click)",
                        RawData = "TIKLANDI",
                        IsPressed = true,
                        Timestamp = DateTime.Now
                    };
                    SignalCaptured?.Invoke(signal);
                }
            }
            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }
    }
}
