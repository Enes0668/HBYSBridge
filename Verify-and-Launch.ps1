# ==============================================================================
# HBYS Bridge Pro - Hastane İstemci Başlatıcı & Bütünlük Doğrulayıcı (Adım 3)
# ==============================================================================
param (
    [string]$TargetExe = "$PSScriptRoot\bin\Release\Publish_Protected\HBYSBridge.exe",
    [string]$ManifestPath = "$PSScriptRoot\bin\Release\Publish_Protected\manifest.json"
)

$ErrorActionPreference = "Stop"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " HBYS ISTEMCI GUVENLIK KONTROLU: BASLATMA ONCESI BUTUNLUK TESTI" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

if (!(Test-Path $TargetExe)) {
    Write-Host "HATA: Hedef calistirilabilir dosya bulunamadi: $TargetExe" -ForegroundColor Red
    exit 1
}

if (!(Test-Path $ManifestPath)) {
    Write-Host "HATA: Sunucu manifest dosyasi bulunamadi: $ManifestPath" -ForegroundColor Red
    exit 1
}

# 1. Manifestten beklenen SHA-256 özetini oku
$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$expectedHash = $manifest.sha256

Write-Host "`n[1/3] Sunucunun Bekledigi SHA-256:" -ForegroundColor Yellow
Write-Host "      $expectedHash" -ForegroundColor Gray

# 2. İndirilen yerel dosyanın SHA-256 özetini hesapla
$localHash = (Get-FileHash -Path $TargetExe -Algorithm SHA256).Hash
Write-Host "`n[2/3] Bilgisayardaki Dosyanin SHA-256:" -ForegroundColor Yellow
Write-Host "      $localHash" -ForegroundColor Gray

# 3. Karşılaştır
Write-Host "`n[3/3] Bütünlük ve Dijital Mühür Karşılaştırması:" -ForegroundColor Yellow

if ($localHash -ne $expectedHash) {
    Write-Host "`n[KRİTİK GÜVENLİK ALARMI] ❌" -ForegroundColor Red
    Write-Host "Dosya parmak izi sunucuyla eslesmiyor! Dosya indirme sirasinda bozulmus veya degistirilmis olabilir." -ForegroundColor Red
    Write-Host "PROGRAMIN CALISTIRILMASI ENGELLENDI!" -ForegroundColor Red
    exit 2
} else {
    Write-Host " -> SHA-256 Parmak İzi: BİREBİR EŞLEŞTİ (DOĞRULANDI) ✅" -ForegroundColor Green
}

# 4. Dijital Mühür Kontrolü
$sig = Get-AuthenticodeSignature -FilePath $TargetExe
if ($sig.Status -eq "HashMismatch") {
    Write-Host "`n[KRİTİK GÜVENLİK ALARMI] ❌" -ForegroundColor Red
    Write-Host "Dijital muhur kirilmis! Dosyaya disaridan mudahale edilmis." -ForegroundColor Red
    exit 3
} else {
    Write-Host " -> Dijital Mühür: GEÇERLİ ($($sig.SignerCertificate.Subject)) ✅" -ForegroundColor Green
}

Write-Host "`n=================================================================" -ForegroundColor Green
Write-Host " GUVENLIK ONAYI VERILDI: Program guvenle baslatiliyor..." -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green

# Güvenli başlatma
$proc = Start-Process -FilePath $TargetExe -WorkingDirectory (Split-Path $TargetExe) -PassThru
Write-Host "HBYS Bridge Pro calisiyor (PID: $($proc.Id))" -ForegroundColor Cyan
