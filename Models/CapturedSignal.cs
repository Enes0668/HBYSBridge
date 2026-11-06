using System;

namespace HBYSBridge.Models
{
    public class CapturedSignal
    {
        public string DeviceType { get; set; } = ""; // "Oyun Kolu (Snopy/Xbox)", "Seri Port (COM)", "Klavye"
        public string DeviceName { get; set; } = ""; // "Snopy Gamepad", "COM3 (Ayak Pedalı)"
        public string SignalId { get; set; } = "";   // "Snopy - R1", "COM3 - CTS (Pedal Kontak)"
        public string RawData { get; set; } = "";    // "Button 6 Down", "Pin CTS = HIGH"
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsPressed { get; set; }
    }
}
