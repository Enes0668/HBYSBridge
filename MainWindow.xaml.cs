using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using HBYSBridge.Models;
using HBYSBridge.Services;

namespace HBYSBridge
{
    public partial class MainWindow : Window
    {
        private readonly UniversalSnifferService _sniffer = new();
        private readonly LocalBridgeServer _server = new();

        public ObservableCollection<CommandMapping> Mappings { get; set; } = new();
        private CommandMapping? _assigningCommand = null;
        private readonly string _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            InitializeDefaultMappings();
            LoadSettings();

            ListCommands.ItemsSource = Mappings;

            _sniffer.SignalCaptured += OnSignalCaptured;
            _sniffer.LogMessage += AddLog;
            _sniffer.ControllerStatusChanged += OnControllerStatusChanged;
            _server.LogMessageReceived += AddLog;
            _server.CommandTriggeredFromWeb += OnExternalTestCommand;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AddLog("HBYSX Dictation Bridge başlatıldı.");
            _sniffer.Start();
            _server.Start();
            RefreshComPorts();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _sniffer.Stop();
            _server.Stop();
            SaveSettings();
        }

        private void InitializeDefaultMappings()
        {
            // --- GRUP 1: KAYIT ---
            Mappings.Add(new CommandMapping
            {
                IsHeader = true,
                CommandName = "Kayıt"
            });

            Mappings.Add(new CommandMapping
            {
                CommandId = "REC_START_PAUSE",
                CommandName = "Kayıt başla / duraklat / devam",
                VirtualKeyCombo = "Ctrl+Alt+F7",
                DefaultShortcutText = "Klavye · Ctrl+Alt+F7",
                AssignedInput = "Snopy - R1 (Sağ Üst)"
            });

            Mappings.Add(new CommandMapping
            {
                CommandId = "REC_HOLD",
                CommandName = "Kayıt (basılı tut konuş)",
                VirtualKeyCombo = "Ctrl+Alt+F7",
                DefaultShortcutText = "",
                IsHoldCommand = true,
                AssignedInput = "— atanmadı —"
            });

            Mappings.Add(new CommandMapping
            {
                CommandId = "REC_SAVE",
                CommandName = "Kaydı kaydet",
                VirtualKeyCombo = "Ctrl+Alt+F9",
                DefaultShortcutText = "Klavye · Ctrl+Alt+F9",
                AssignedInput = "Snopy - Buton 3 (Çarpı / A)"
            });

            Mappings.Add(new CommandMapping
            {
                CommandId = "REC_CANCEL",
                CommandName = "Kaydı iptal et",
                VirtualKeyCombo = "Ctrl+Alt+F6",
                DefaultShortcutText = "Klavye · Ctrl+Alt+F6",
                AssignedInput = "Snopy - Buton 2 (Daire / B)"
            });

            // --- GRUP 2: DİNLEME ---
            Mappings.Add(new CommandMapping
            {
                IsHeader = true,
                CommandName = "Dinleme"
            });

            Mappings.Add(new CommandMapping
            {
                CommandId = "PLAY_HOLD",
                CommandName = "Dinle (basılı tut)",
                VirtualKeyCombo = "F10",
                DefaultShortcutText = "Klavye · F10",
                IsHoldCommand = true,
                AssignedInput = "— atanmadı —"
            });

            Mappings.Add(new CommandMapping
            {
                CommandId = "PLAY_STOP",
                CommandName = "Dinle / durdur",
                VirtualKeyCombo = "Ctrl+Alt+F5",
                DefaultShortcutText = "Klavye · Ctrl+Alt+F5",
                AssignedInput = "— atanmadı —"
            });

            Mappings.Add(new CommandMapping
            {
                CommandId = "REWIND_HOLD",
                CommandName = "Geri sar (basılı tut)",
                VirtualKeyCombo = "Ctrl+Alt+F11",
                DefaultShortcutText = "Klavye · Ctrl+Alt+F11",
                IsHoldCommand = true,
                AssignedInput = "— atanmadı —"
            });

            Mappings.Add(new CommandMapping
            {
                CommandId = "FORWARD_HOLD",
                CommandName = "İleri sar (basılı tut)",
                VirtualKeyCombo = "Ctrl+Alt+F12",
                DefaultShortcutText = "Klavye · Ctrl+Alt+F12",
                IsHoldCommand = true,
                AssignedInput = "— atanmadı —"
            });

            Mappings.Add(new CommandMapping
            {
                CommandId = "PREV_REC",
                CommandName = "Önceki kayıt",
                VirtualKeyCombo = "Ctrl+Alt+F5",
                DefaultShortcutText = "",
                AssignedInput = "— atanmadı —"
            });

            Mappings.Add(new CommandMapping
            {
                CommandId = "NEXT_REC",
                CommandName = "Sonraki kayıt",
                VirtualKeyCombo = "Ctrl+Alt+F8",
                DefaultShortcutText = "Klavye · Ctrl+Alt+F8",
                AssignedInput = "— atanmadı —"
            });

            Mappings.Add(new CommandMapping
            {
                CommandId = "SLOW_DOWN",
                CommandName = "Yavaşlat",
                DefaultShortcutText = "",
                AssignedInput = "— atanmadı —"
            });

            Mappings.Add(new CommandMapping
            {
                CommandId = "SPEED_UP",
                CommandName = "Hızlandır",
                DefaultShortcutText = "",
                AssignedInput = "— atanmadı —"
            });
        }

        private void OnControllerStatusChanged(string name, bool connected)
        {
            Dispatcher.Invoke(() =>
            {
                TxtDeviceStatus.Text = connected ? name : "Cihaz bulunamadı. Pedalı / SpeechMike'ı takın.";
                TxtGamepadName.Text = connected ? $"✅ {name}" : "❌ Cihaz bağlı değil.";
            });
        }

        private void OnSignalCaptured(CapturedSignal signal)
        {
            Dispatcher.Invoke(() =>
            {
                if (signal.DeviceType.Contains("Oyun Kolu"))
                {
                    TxtLiveGamepadButton.Text = signal.IsPressed ? $"🟢 {signal.SignalId} (BASILDI)" : $"⚪ {signal.SignalId} (BIRAKILDI)";
                }
                else if (signal.DeviceType.Contains("Seri Port"))
                {
                    TxtSerialStatus.Text = signal.IsPressed ? $"🟢 {signal.SignalId} (BASILDI)" : "Pedal bırakıldı.";
                }

                if (_assigningCommand != null && signal.IsPressed)
                {
                    _assigningCommand.AssignedInput = signal.SignalId;
                    AddLog($"[Atama] '{_assigningCommand.CommandName}' komutuna '{signal.SignalId}' atandı.");
                    _assigningCommand = null;
                    TxtAssignPrompt.Text = "";
                    SaveSettings();
                    return;
                }

                var mapping = Mappings.FirstOrDefault(m => !m.IsHeader && m.AssignedInput.Equals(signal.SignalId, StringComparison.OrdinalIgnoreCase));
                if (mapping != null)
                {
                    if (mapping.IsHoldCommand)
                    {
                        KeyboardSimulator.SendKeyCombo(mapping.VirtualKeyCombo, signal.IsPressed);
                        AddLog(signal.IsPressed
                            ? $"[KOMUT BASILI TUTULUYOR] {mapping.CommandName} ({mapping.VirtualKeyCombo})"
                            : $"[KOMUT BIRAKILDI] {mapping.CommandName}");
                    }
                    else if (signal.IsPressed)
                    {
                        KeyboardSimulator.TapKeyCombo(mapping.VirtualKeyCombo);
                        AddLog($"[KOMUT ÇALIŞTI] {mapping.CommandName} -> Klavye '{mapping.VirtualKeyCombo}' gönderildi.");
                    }
                }
            });
        }

        private void OnExternalTestCommand(string device)
        {
            Dispatcher.Invoke(() =>
            {
                if (device.Equals("pedal", StringComparison.OrdinalIgnoreCase))
                {
                    _sniffer.SimulateComPedalPress("COM3");
                }
                else if (device.Equals("xbox", StringComparison.OrdinalIgnoreCase))
                {
                    _sniffer.SimulateXboxButtonPress("Xbox - A Tuşu");
                }
            });
        }

        private void BtnAssign_Click(object sender, RoutedEventArgs e)
        {
            if (ListCommands.SelectedItem is CommandMapping selected && !selected.IsHeader)
            {
                _assigningCommand = selected;
                TxtAssignPrompt.Text = $"'{selected.CommandName}' için koldan bir tuşa veya pedala basın...";
            }
            else
            {
                MessageBox.Show("Lütfen atamak istediğiniz bir komut seçin.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnRemoveAssign_Click(object sender, RoutedEventArgs e)
        {
            if (ListCommands.SelectedItem is CommandMapping selected && !selected.IsHeader)
            {
                selected.AssignedInput = "— atanmadı —";
                SaveSettings();
                AddLog($"[Atama Kaldırıldı] {selected.CommandName}");
            }
        }

        private void RefreshComPorts()
        {
            CmbPorts.Items.Clear();
            var ports = System.IO.Ports.SerialPort.GetPortNames();
            foreach (var p in ports) CmbPorts.Items.Add(p);
            if (CmbPorts.Items.Count > 0) CmbPorts.SelectedIndex = 0;
        }

        private void BtnRefreshPorts_Click(object sender, RoutedEventArgs e) => RefreshComPorts();

        private void BtnConnectPort_Click(object sender, RoutedEventArgs e)
        {
            if (CmbPorts.SelectedItem != null)
            {
                string port = CmbPorts.SelectedItem.ToString()!;
                TxtSerialStatus.Text = $"{port} dinleniyor.";
                AddLog($"[Seri Port] {port} bağlandı.");
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e) => ListLogs.Items.Clear();

        private void AddLog(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            if (Dispatcher.CheckAccess())
            {
                ListLogs.Items.Add(line);
                if (ListLogs.Items.Count > 100) ListLogs.Items.RemoveAt(0);
                ListLogs.ScrollIntoView(line);
            }
            else
            {
                Dispatcher.Invoke(() => AddLog(msg));
            }
        }

        private void SaveSettings()
        {
            try
            {
                var dict = Mappings.Where(m => !m.IsHeader).ToDictionary(m => m.CommandId, m => m.AssignedInput);
                File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var dict = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(json);
                    if (dict != null)
                    {
                        foreach (var m in Mappings.Where(m => !m.IsHeader))
                        {
                            if (dict.TryGetValue(m.CommandId, out var val))
                            {
                                m.AssignedInput = val;
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
