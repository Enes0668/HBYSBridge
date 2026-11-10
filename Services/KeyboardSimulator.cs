using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace HBYSBridge.Services
{
    public static class KeyboardSimulator
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

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

        /// <summary>
        /// Akıllı pencere koruması: Açık olduğunda yalnızca HBYS, PACS, kamera veya web tarayıcıları odaktayken tuş gönderilir.
        /// </summary>
        public static bool EnableWindowGuard { get; set; } = true;

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
        /// Aktif olan (odaktaki) pencerenin başlığını ve işlem adını döndürür.
        /// </summary>
        public static (string Title, string ProcessName) GetActiveWindowInfo()
        {
            IntPtr hWnd = GetForegroundWindow();
            if (hWnd == IntPtr.Zero) return (string.Empty, string.Empty);

            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();

            string procName = string.Empty;
            try
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != 0)
                {
                    using var proc = Process.GetProcessById((int)pid);
                    procName = proc.ProcessName;
                }
            }
            catch
            {
                // Process bilgisi alınamazsa sessizce devam et
            }

            return (title, procName);
        }

        /// <summary>
        /// Odaktaki pencerenin geçerli bir tıbbi / HBYS / tarayıcı penceresi olup olmadığını denetler.
        /// </summary>
        public static bool IsAllowedWindowActive(out string activeTitle, out string activeProcess)
        {
            var (title, proc) = GetActiveWindowInfo();
            activeTitle = title;
            activeProcess = proc;

            // Koruma kapatılmışsa her pencereye izin ver
            if (!EnableWindowGuard) return true;

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(proc))
                return false;

            string lowerTitle = title.ToLowerInvariant();
            string lowerProc = proc.ToLowerInvariant();

            // 1. Web Tarayıcıları (HBYS/PACS web arayüzü sekmesi)
            if (lowerProc.Contains("chrome") || lowerProc.Contains("msedge") ||
                lowerProc.Contains("firefox") || lowerProc.Contains("brave") ||
                lowerProc.Contains("opera"))
            {
                return true;
            }

            // 2. Tıbbi / HBYS / PACS / Dikte / Endoskopi Masaüstü Uygulamaları
            string[] allowedKeywords = new[]
            {
                "hbys", "pacs", "hastane", "enlil", "karmed", "fonets", "akgun", "akgün",
                "siso", "cozum", "çözüm", "bilsis", "endoskopi", "endoscopy", "kamera",
                "camera", "dicom", "dictation", "dikte", "ses", "mikrofon",
                "hbysbridge", "dictationbridge"
            };

            foreach (var kw in allowedKeywords)
            {
                if (lowerTitle.Contains(kw) || lowerProc.Contains(kw))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Kısayol kombinasyonunu basar veya bırakır (örn: "Ctrl+Alt+F7").
        /// Tuş basımı sırasında hedef pencere korunur.
        /// </summary>
        public static bool SendKeyCombo(string comboText, bool isPress, out string activeTitle, out string activeProcess)
        {
            activeTitle = string.Empty;
            activeProcess = string.Empty;

            if (string.IsNullOrWhiteSpace(comboText)) return false;

            // Tuşa basarken aktif pencereyi denetle
            if (isPress)
            {
                if (!IsAllowedWindowActive(out activeTitle, out activeProcess))
                {
                    // Güvensiz pencere (örn: Word, Excel, Masaüstü) -> Engelle!
                    return false;
                }
            }

            var parts = comboText.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var keysToSend = new List<byte>();

            foreach (var part in parts)
            {
                if (KeyMap.TryGetValue(part.Trim(), out byte vk))
                {
                    keysToSend.Add(vk);
                }
            }

            if (keysToSend.Count == 0) return false;

            if (isPress)
            {
                foreach (var vk in keysToSend)
                {
                    keybd_event(vk, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                }
            }
            else
            {
                keysToSend.Reverse();
                foreach (var vk in keysToSend)
                {
                    keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            }

            return true;
        }

        /// <summary>
        /// Tek seferlik tıkla-bırak simülasyonu
        /// </summary>
        public static bool TapKeyCombo(string comboText, out string activeTitle, out string activeProcess)
        {
            if (!SendKeyCombo(comboText, true, out activeTitle, out activeProcess))
            {
                return false;
            }

            System.Threading.Thread.Sleep(50);
            SendKeyCombo(comboText, false, out _, out _);
            return true;
        }
    }
}
