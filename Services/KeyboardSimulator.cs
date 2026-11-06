using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace HBYSBridge.Services
{
    public static class KeyboardSimulator
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        // Virtual Key Codes
        private const byte VK_CONTROL = 0x11;
        private const byte VK_MENU = 0x12; // ALT
        private const byte VK_SHIFT = 0x10;

        private const byte VK_F5 = 0x74;
        private const byte VK_F6 = 0x75;
        private const byte VK_F7 = 0x76;
        private const byte VK_F8 = 0x77;
        private const byte VK_F9 = 0x78;
        private const byte VK_F10 = 0x79;
        private const byte VK_F11 = 0x7A;
        private const byte VK_F12 = 0x7B;

        private static readonly Dictionary<string, byte> KeyMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Ctrl", VK_CONTROL },
            { "Control", VK_CONTROL },
            { "Alt", VK_MENU },
            { "Shift", VK_SHIFT },
            { "F5", VK_F5 },
            { "F6", VK_F6 },
            { "F7", VK_F7 },
            { "F8", VK_F8 },
            { "F9", VK_F9 },
            { "F10", VK_F10 },
            { "F11", VK_F11 },
            { "F12", VK_F12 }
        };

        /// <summary>
        /// Kısayol kombinasyonunu basar veya bırakır (örn: "Ctrl+Alt+F7")
        /// </summary>
        public static void SendKeyCombo(string comboText, bool isPress)
        {
            if (string.IsNullOrWhiteSpace(comboText)) return;

            var parts = comboText.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var keysToSend = new List<byte>();

            foreach (var part in parts)
            {
                if (KeyMap.TryGetValue(part.Trim(), out byte vk))
                {
                    keysToSend.Add(vk);
                }
            }

            if (keysToSend.Count == 0) return;

            if (isPress)
            {
                // Tuşlara sırayla bas
                foreach (var vk in keysToSend)
                {
                    keybd_event(vk, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                }
            }
            else
            {
                // Tuşları ters sırayla bırak
                keysToSend.Reverse();
                foreach (var vk in keysToSend)
                {
                    keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            }
        }

        /// <summary>
        /// Tek seferlik tıkla-bırak simülasyonu
        /// </summary>
        public static void TapKeyCombo(string comboText)
        {
            SendKeyCombo(comboText, true);
            System.Threading.Thread.Sleep(50);
            SendKeyCombo(comboText, false);
        }
    }
}
