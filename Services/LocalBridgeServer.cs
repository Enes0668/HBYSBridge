using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HBYSBridge.Services
{
    public class LocalBridgeServer
    {
        private HttpListener? _listener;
        private bool _isRunning;
        private readonly int _port = 4547;

        public event Action<string>? LogMessageReceived;
        public event Action<string>? CommandTriggeredFromWeb;

        public bool IsRunning => _isRunning;

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
                _listener.Start();
                _isRunning = true;

                LogMessageReceived?.Invoke($"[Sunucu] Port {_port} üzerinde dinleme başladı (HBYS Web Entegrasyonu).");

                Task.Run(ListenLoop);
            }
            catch (Exception ex)
            {
                LogMessageReceived?.Invoke($"[Sunucu Hatası] Port {_port} başlatılamadı: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }
        }

        private async Task ListenLoop()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    ProcessRequest(context);
                }
                catch
                {
                    if (!_isRunning) break;
                }
            }
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;

            // CORS başlıkları (HBYS web arayüzü tarayıcıdan bağlandığı için)
            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 200;
                res.Close();
                return;
            }

            string responseJson = "{\"status\":\"ok\",\"service\":\"HBYSX Dictation Bridge\",\"port\":4547}";

            if (req.Url?.AbsolutePath.Contains("/status") == true)
            {
                responseJson = "{\"status\":\"running\",\"device\":\"Connected\",\"screens\":1}";
            }
            else if (req.Url?.AbsolutePath.Contains("/simulate") == true || req.Url?.AbsolutePath.Contains("/test") == true)
            {
                string dev = req.QueryString["device"] ?? "pedal";
                CommandTriggeredFromWeb?.Invoke(dev);
                responseJson = $"{{\"status\":\"simulated\",\"device\":\"{dev}\",\"timestamp\":\"{DateTime.Now:O}\"}}";
                LogMessageReceived?.Invoke($"[Harici Test API] Dışarıdan '{dev}' sinyali enjekte edildi.");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
            res.ContentType = "application/json";
            res.ContentLength64 = buffer.Length;
            res.OutputStream.Write(buffer, 0, buffer.Length);
            res.Close();
        }
    }
}
