<#
.SYNOPSIS
    HBYSX Dictation Bridge - Harici Donanım Simülatörü ve Test Paketi
#>

param (
    [ValidateSet("Menu", "1A", "1B", "2", "3", "4", "5", "All")]
    [string]$Test = "Menu"
)

function Show-Header {
    Clear-Host
    Write-Host "==================================================================" -ForegroundColor Cyan
    Write-Host "      HBYSX DONANIM KÖPRÜSÜ - HARİCİ TEST VE SİMÜLASYON ARACI     " -ForegroundColor Yellow
    Write-Host "==================================================================" -ForegroundColor Cyan
    Write-Host ""
}

function Test-WebPort {
    try {
        $res = Invoke-RestMethod -Uri "http://localhost:4547/status" -TimeoutSec 2 -ErrorAction Stop
        return $true
    } catch {
        return $false
    }
}

function Run-Scenario1A {
    Write-Host "`n[TEST 1A] Senaryo 1A: Fiziksel USB Oyun Kolu (DirectInput / Snopy)" -ForegroundColor Yellow
    Write-Host "------------------------------------------------------------------"
    Write-Host "Masadaki fiziksel Snopy kol ile test:"
    Write-Host "-> Kolun herhangi bir tuşuna (örneğin R1 veya Buton 3) basın."
    Write-Host "-> Ana programın 'Log' sekmesinde komutun çalıştığını doğrulayın."
    Write-Host "[BİLGİ] Fiziksel DirectInput testi hazır." -ForegroundColor Green
}

function Run-Scenario1B {
    Write-Host "`n[TEST 1B] Senaryo 1B: Harici Xbox 360 Kumandası (XInput Modu)" -ForegroundColor Yellow
    Write-Host "------------------------------------------------------------------"
    Write-Host "Dışarıdan sanal bir Xbox kumandasının 'A' tuşuna basılma sinyali fırlatılıyor..." -ForegroundColor Cyan
    
    if (-not (Test-WebPort)) {
        Write-Host "[-] HATA: HBYSBridge programı açık değil! Lütfen önce programı başlatın." -ForegroundColor Red
        return
    }

    try {
        $res = Invoke-RestMethod -Uri "http://localhost:4547/simulate?device=xbox" -TimeoutSec 3
        Write-Host "[+] BAŞARILI: Harici 'Xbox - A Tuşu' sinyali enjekte edildi!" -ForegroundColor Green
        Write-Host "    -> Ana programın Log sekmesini kontrol edin."
    } catch {
        Write-Host "[-] Sinyal gönderilemedi: $_" -ForegroundColor Red
    }
}

function Run-Scenario2 {
    Write-Host "`n[TEST 2] Senaryo 2: Seri Port / COM Port Ayak Pedalı (Hualin HL-300)" -ForegroundColor Yellow
    Write-Host "------------------------------------------------------------------"
    Write-Host "Dışarıdan sanal COM3 portundaki ayak pedalına elektrik kontağı gönderiliyor..." -ForegroundColor Cyan
    
    if (-not (Test-WebPort)) {
        Write-Host "[-] HATA: HBYSBridge programı açık değil! Lütfen önce programı başlatın." -ForegroundColor Red
        return
    }

    try {
        $res = Invoke-RestMethod -Uri "http://localhost:4547/simulate?device=pedal" -TimeoutSec 3
        Write-Host "[+] BAŞARILI: Harici 'COM3 - CTS (Pedal Kontak)' sinyali enjekte edildi!" -ForegroundColor Green
        Write-Host "    -> Ana programın Log sekmesinde 'KOMUT BASILI TUTULUYOR / ÇALIŞTI' yazısını kontrol edin."
    } catch {
        Write-Host "[-] Sinyal gönderilemedi: $_" -ForegroundColor Red
    }
}

function Run-Scenario3 {
    Write-Host "`n[TEST 3] Senaryo 3: USB HID Klavye Modundaki Ayak Pedalı (PCsensor/iKKEGOL)" -ForegroundColor Yellow
    Write-Host "------------------------------------------------------------------"
    Write-Host "Windows çekirdeği seviyesinde donanımsal F12 pedal vuruşu fırlatılıyor..." -ForegroundColor Cyan
    
    $code = @"
    using System;
    using System.Runtime.InteropServices;
    public class HardwareInput {
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        public static void PressF12() {
            keybd_event(0x7B, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(100);
            keybd_event(0x7B, 0, 2, UIntPtr.Zero);
        }
    }
"@
    Add-Type -TypeDefinition $code -ErrorAction SilentlyContinue
    [HardwareInput]::PressF12()
    
    Write-Host "[+] BAŞARILI: Donanımsal F12 sinyali sisteme enjekte edildi!" -ForegroundColor Green
    Write-Host "    -> Ana programın Log sekmesini kontrol edin."
}

function Run-Scenario5 {
    Write-Host "`n[TEST 5] Baş Mühendisin Dictation Bridge Hub'ını Test Et (SignalR / WebSocket)" -ForegroundColor Yellow
    Write-Host "------------------------------------------------------------------"
    Write-Host "1. Baş Mühendisin uygulaması (Port 4547) kontrol ediliyor..." -ForegroundColor Cyan
    
    $chiefProc = Get-Process -Name "HBYSX.ConsoleApps.DictationBridge" -ErrorAction SilentlyContinue
    if (-not $chiefProc) {
        Write-Host "[-] UYARI: Masaüstündeki 'HBYSX.ConsoleApps.DictationBridge.exe' açık görünmüyor!" -ForegroundColor Yellow
        Write-Host "    Lütfen masaüstündeki Dictation Bridge uygulamasını başlatıp tekrar deneyin."
        return
    }
    Write-Host " [+] Baş Mühendisin uygulaması aktif! (PID: $($chiefProc.Id))" -ForegroundColor Green

    Write-Host "2. SignalR Hub'ına (ws://127.0.0.1:4547/dictationHub) 'Dikte Ekranı' olarak bağlanılıyor..." -ForegroundColor Cyan
    
    $ws = New-Object System.Net.WebSockets.ClientWebSocket
    $cts = New-Object System.Threading.CancellationTokenSource
    $cts.CancelAfter(25000) # 25 saniye dinleme süresi

    try {
        $uri = New-Object System.Uri("ws://127.0.0.1:4547/dictationHub")
        $connectTask = $ws.ConnectAsync($uri, $cts.Token)
        $connectTask.Wait(3000)

        if ($ws.State -ne [System.Net.WebSockets.WebSocketState]::Open) {
            Write-Host "[-] WebSocket bağlantısı açılamadı (Durum: $($ws.State))" -ForegroundColor Red
            return
        }

        # SignalR Handshake gönder
        $handshake = '{"protocol":"json","version":1}' + [char]0x1E
        $handshakeBytes = [System.Text.Encoding]::UTF8.GetBytes($handshake)
        $segment = [System.ArraySegment[byte]]::new($handshakeBytes)
        $ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, $cts.Token).Wait(2000)

        # Handshake cevabını oku
        $buf = New-Object byte[] 4096
        $recvSeg = [System.ArraySegment[byte]]::new($buf)
        $ws.ReceiveAsync($recvSeg, $cts.Token).Wait(2000)

        Write-Host " [+] SIGNALR BAĞLANTISI KURULDU! ✅" -ForegroundColor Green
        Write-Host "     -> Baş mühendisin loguna 'Screen connected' kaydı düşürüldü." -ForegroundColor Gray
        Write-Host "`n>>> ŞİMDİ MASADAKİ PEDALA VEYA KUMANDAYA BASIN (Dinleniyor - 20 sn) <<<" -ForegroundColor Magenta
        Write-Host "(Testi erken bitirmek için 'Q' tuşuna basabilirsiniz)`n" -ForegroundColor DarkGray

        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        while ($ws.State -eq [System.Net.WebSockets.WebSocketState]::Open -and $stopwatch.Elapsed.TotalSeconds -lt 20) {
            try {
                if ([Console]::KeyAvailable) {
                    $key = [Console]::ReadKey($true)
                    if ($key.Key -eq [ConsoleKey]::Q) { break }
                }
            } catch {}

            $readTask = $ws.ReceiveAsync($recvSeg, $cts.Token)
            $completedIndex = [System.Threading.Tasks.Task]::WaitAny(@($readTask), 500)

            if ($completedIndex -eq 0 -and $readTask.Status -eq "RanToCompletion") {
                $count = $readTask.Result.Count
                if ($count -gt 0) {
                    $rawMsg = [System.Text.Encoding]::UTF8.GetString($buf, 0, $count).Trim([char]0x1E)
                    Write-Host "[+] CANLI SİNYAL ALINDI: $rawMsg" -ForegroundColor Green
                }
            }
        }
    } catch {
        Write-Host "[-] Bağlantı hatası: $_" -ForegroundColor Red
    } finally {
        if ($ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
            $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "Test bitti", [System.Threading.CancellationToken]::None).Wait(2000)
            Write-Host "`n[+] Bağlantı düzgünce kapatıldı ('Screen disconnected' loglandı)." -ForegroundColor Gray
        }
        $ws.Dispose()
    }

    # Log dosyasını göster
    $logPath = "C:\Users\Enes Melih Eroğlu\Desktop\Dictation Bridge\logs\dictation-bridge.log"
    if (Test-Path $logPath) {
        Write-Host "`n--- Baş Mühendisin Uygulamasındaki Son Log Satırları ---" -ForegroundColor Cyan
        Get-Content $logPath -Tail 6 | ForEach-Object { Write-Host "   $_" -ForegroundColor White }
    }
}

function Run-All {
    Show-Header
    Write-Host "TÜM SENARYOLARIN OTOMATİK DOĞRULAMA TESTİ BAŞLATILIYOR..." -ForegroundColor Magenta
    
    if (-not (Test-WebPort)) {
        Write-Host "`n[-] KRİTİK: HBYSBridge.exe açık değil! Testi çalıştırmadan önce programı açmalısınız." -ForegroundColor Red
        return
    }

    Run-Scenario1B
    Start-Sleep -Seconds 1
    Run-Scenario2
    Start-Sleep -Seconds 1
    Run-Scenario3
    Start-Sleep -Seconds 1
    Run-Scenario1A

    Write-Host "`n==================================================================" -ForegroundColor Cyan
    Write-Host "                 TÜM TESTLER TAMAMLANDI                           " -ForegroundColor Green
    Write-Host "==================================================================" -ForegroundColor Cyan
}

# --- MENÜ ---
if ($Test -eq "Menu") {
    Show-Header
    Write-Host "Test etmek istediğiniz donanım senaryosunu seçin:" -ForegroundColor White
    Write-Host "  1A - Senaryo 1A: Snopy USB Oyun Kolu (Fiziksel DirectInput)"
    Write-Host "  1B - Senaryo 1B: Xbox 360 Kumandası (Harici XInput Simülasyonu)"
    Write-Host "  2  - Senaryo 2:  Seri Port Ayak Pedalı (COM3 / CTS Kontak)"
    Write-Host "  3  - Senaryo 3:  USB HID Klavye Modundaki Ayak Pedalı"
    Write-Host "  4  - Bizim Programın Tüm Testlerini Çalıştır (HBYSBridge)"
    Write-Host "  5  - BAŞ MÜHENDİSİN UYGULAMASINI TEST ET (SignalR / Dictation Bridge)" -ForegroundColor Green
    Write-Host "  Q  - Çıkış"
    Write-Host ""
    $choice = Read-Host "Seçiminiz (1A, 1B, 2, 3, 4, 5)"

    switch ($choice.ToUpper()) {
        "1A" { Run-Scenario1A }
        "1B" { Run-Scenario1B }
        "2"  { Run-Scenario2 }
        "3"  { Run-Scenario3 }
        "4"  { Run-All }
        "5"  { Run-Scenario5 }
        default { Write-Host "Çıkış yapıldı." }
    }
} elseif ($Test -eq "All" -or $Test -eq "4") {
    Run-All
} elseif ($Test -eq "1A") {
    Run-Scenario1A
} elseif ($Test -eq "1B") {
    Run-Scenario1B
} elseif ($Test -eq "2") {
    Run-Scenario2
} elseif ($Test -eq "3") {
    Run-Scenario3
} elseif ($Test -eq "5") {
    Run-Scenario5
}
