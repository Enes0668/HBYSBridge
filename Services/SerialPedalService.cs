using System;
using System.IO.Ports;

namespace HBYSBridge.Services
{
    public class SerialPedalService
    {
        private SerialPort? _serialPort;
        public event Action<string, bool>? PedalStateChanged; // (PinName/Data, isPressed)
        public event Action<string>? LogMessage;

        public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

        public string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }

        public bool Connect(string portName, int baudRate = 9600)
        {
            Disconnect();
            try
            {
                _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    DtrEnable = true,
                    RtsEnable = true
                };

                _serialPort.PinChanged += SerialPort_PinChanged;
                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                LogMessage?.Invoke($"[Seri Port] {portName} bağlantısı başarılı.");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[Seri Port Hatası] {portName} açılamadı: {ex.Message}");
                return false;
            }
        }

        public void Disconnect()
        {
            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen) _serialPort.Close();
                    _serialPort.Dispose();
                }
                catch { }
                _serialPort = null;
            }
        }

        private void SerialPort_PinChanged(object sender, SerialPinChangedEventArgs e)
        {
            if (_serialPort == null) return;

            // Ayak pedalları çoğunlukla CTS veya DSR hattını kısa devre eder
            if (e.EventType == SerialPinChange.CtsChanged)
            {
                PedalStateChanged?.Invoke("Pedal (CTS)", _serialPort.CtsHolding);
            }
            else if (e.EventType == SerialPinChange.DsrChanged)
            {
                PedalStateChanged?.Invoke("Pedal (DSR)", _serialPort.DsrHolding);
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    int bytesToRead = _serialPort.BytesToRead;
                    byte[] buffer = new byte[bytesToRead];
                    _serialPort.Read(buffer, 0, bytesToRead);

                    // Gelen veri varsa pedal basıldı kabul et
                    PedalStateChanged?.Invoke("Pedal (Veri)", true);
                }
            }
            catch { }
        }
    }
}
