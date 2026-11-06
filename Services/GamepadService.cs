using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace HBYSBridge.Services
{
    public class GamepadService
    {
        // --- XINPUT DEFINITIONS ---
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState14(int dwUserIndex, ref XINPUT_STATE pState);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        private static extern int XInputGetState91(int dwUserIndex, ref XINPUT_STATE pState);

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        private static readonly Dictionary<ushort, string> XboxButtonNames = new()
        {
            { 0x1000, "Xbox - A Tuşu" },
            { 0x2000, "Xbox - B Tuşu" },
            { 0x4000, "Xbox - X Tuşu" },
            { 0x8000, "Xbox - Y Tuşu" },
            { 0x0100, "Xbox - Sol Omuz (LB)" },
            { 0x0200, "Xbox - Sağ Omuz (RB)" },
            { 0x0010, "Xbox - Başlat (Start)" },
            { 0x0020, "Xbox - Geri (Back)" },
            { 0x0001, "Xbox - D-Pad Yukarı" },
            { 0x0002, "Xbox - D-Pad Aşağı" },
            { 0x0004, "Xbox - D-Pad Sol" },
            { 0x0008, "Xbox - D-Pad Sağ" },
            { 0x0040, "Xbox - Sol Çubuk Tık (LS)" },
            { 0x0080, "Xbox - Sağ Çubuk Tık (RS)" }
        };

        // --- WINMM DEFINITIONS (DirectInput / Snopy vb.) ---
        [DllImport("winmm.dll")]
        private static extern int joyGetNumDevs();

        [DllImport("winmm.dll")]
        private static extern int joyGetPosEx(int uJoyID, ref JOYINFOEX pji);

        [StructLayout(LayoutKind.Sequential)]
        private struct JOYINFOEX
        {
            public int dwSize;
            public int dwFlags;
            public int dwXpos;
            public int dwYpos;
            public int dwZpos;
            public int dwRpos;
            public int dwUpos;
            public int dwVpos;
            public int dwButtons;
            public int dwButtonNumber;
            public int dwPOV;
            public int dwReserved1;
            public int dwReserved2;
        }

        private const int JOY_RETURNBUTTONS = 0x00000080;
        private const int JOYERR_NOERROR = 0;

        // EVENTS
        public event Action<string, bool>? ButtonStateChanged; // (ButtonName, isPressed)
        public event Action<string, bool>? ControllerConnected; // (DeviceName, isConnected)

        private Thread? _pollThread;
        private bool _isRunning;
        private string _activeDeviceName = "Cihaz aranıyor...";
        private bool _isDeviceConnected = false;

        private int _lastRawButtons = 0;
        private bool _lastLeftTrigger = false;
        private bool _lastRightTrigger = false;

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _pollThread = new Thread(PollLoop)
            {
                IsBackground = true,
                Name = "GamepadPollWorker"
            };
            _pollThread.Start();
        }

        public void Stop()
        {
            _isRunning = false;
        }

        private void PollLoop()
        {
            JOYINFOEX joyInfo = new JOYINFOEX();
            joyInfo.dwSize = Marshal.SizeOf(typeof(JOYINFOEX));
            joyInfo.dwFlags = JOY_RETURNBUTTONS;

            while (_isRunning)
            {
                bool foundThisTick = false;
                string currentDevName = "";

                // 1. Önce XInput (Xbox Modu) kontrol et
                XINPUT_STATE xState = new XINPUT_STATE();
                int xResult = -1;
                try
                {
                    xResult = XInputGetState14(0, ref xState);
                }
                catch
                {
                    try { xResult = XInputGetState91(0, ref xState); } catch { }
                }

                if (xResult == 0) // Xbox kolu bağlı!
                {
                    foundThisTick = true;
                    currentDevName = "Xbox 360 / XInput Oyun Kolu";
                    ProcessXboxButtons(xState);
                }
                else
                {
                    // 2. DirectInput / Standart Joystick (Snopy vb.) kontrol et
                    for (int id = 0; id < 8; id++)
                    {
                        if (joyGetPosEx(id, ref joyInfo) == JOYERR_NOERROR)
                        {
                            foundThisTick = true;
                            currentDevName = $"Snopy / USB Oyun Kolu (Port {id})";
                            ProcessDirectInputButtons(joyInfo.dwButtons);
                            break;
                        }
                    }
                }

                if (foundThisTick != _isDeviceConnected || currentDevName != _activeDeviceName)
                {
                    _isDeviceConnected = foundThisTick;
                    _activeDeviceName = foundThisTick ? currentDevName : "Cihaz bulunamadı. Kolu takın.";
                    ControllerConnected?.Invoke(_activeDeviceName, _isDeviceConnected);
                }

                Thread.Sleep(30); // 30ms = ~33 FPS tepki süresi
            }
        }

        private void ProcessXboxButtons(XINPUT_STATE state)
        {
            int currentButtons = state.Gamepad.wButtons;

            foreach (var kvp in XboxButtonNames)
            {
                bool wasPressed = (_lastRawButtons & kvp.Key) != 0;
                bool isPressed = (currentButtons & kvp.Key) != 0;

                if (isPressed != wasPressed)
                {
                    ButtonStateChanged?.Invoke(kvp.Value, isPressed);
                }
            }

            // Tetikler (LT / RT)
            bool ltPressed = state.Gamepad.bLeftTrigger > 80;
            if (ltPressed != _lastLeftTrigger)
            {
                _lastLeftTrigger = ltPressed;
                ButtonStateChanged?.Invoke("Xbox - Sol Tetik (LT)", ltPressed);
            }

            bool rtPressed = state.Gamepad.bRightTrigger > 80;
            if (rtPressed != _lastRightTrigger)
            {
                _lastRightTrigger = rtPressed;
                ButtonStateChanged?.Invoke("Xbox - Sağ Tetik (RT)", rtPressed);
            }

            _lastRawButtons = currentButtons;
        }

        private void ProcessDirectInputButtons(int buttons)
        {
            if (buttons == _lastRawButtons) return;

            for (int i = 0; i < 16; i++)
            {
                int mask = 1 << i;
                bool wasPressed = (_lastRawButtons & mask) != 0;
                bool isPressed = (buttons & mask) != 0;

                if (isPressed != wasPressed)
                {
                    string buttonName = GetSnopyButtonName(i + 1);
                    ButtonStateChanged?.Invoke(buttonName, isPressed);
                }
            }

            _lastRawButtons = buttons;
        }

        private string GetSnopyButtonName(int buttonIndex)
        {
            return buttonIndex switch
            {
                1 => "Snopy - Buton 1 (Üçgen / Y)",
                2 => "Snopy - Buton 2 (Daire / B)",
                3 => "Snopy - Buton 3 (Çarpı / A)",
                4 => "Snopy - Buton 4 (Kare / X)",
                5 => "Snopy - L1 (Sol Üst)",
                6 => "Snopy - R1 (Sağ Üst)",
                7 => "Snopy - L2 (Sol Alt)",
                8 => "Snopy - R2 (Sağ Alt)",
                9 => "Snopy - Seç (Select)",
                10 => "Snopy - Başlat (Start)",
                11 => "Snopy - L3 (Sol Çubuk)",
                12 => "Snopy - R3 (Sağ Çubuk)",
                _ => $"Snopy - Buton {buttonIndex}"
            };
        }
    }
}
