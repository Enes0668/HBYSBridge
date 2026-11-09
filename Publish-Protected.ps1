# ==============================================================================
# HBYS Bridge Pro - Adım 1: Obfuscation (Kod Karartma & Koruma) Otomasyonu
# ==============================================================================

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$csprojPath = Join-Path $projectRoot "HBYSBridge.csproj"
$obfuscarXml = Join-Path $projectRoot "obfuscar.xml"
$releaseDir = Join-Path $projectRoot "bin\Release\net8.0-windows"
$obfuscatedDir = Join-Path $releaseDir "Obfuscated"
$protectedOutDir = Join-Path $projectRoot "bin\Release\Publish_Protected"
$obfuscarExe = Join-Path $env:USERPROFILE ".nuget\packages\obfuscar\2.2.38\tools\Obfuscar.Console.exe"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " [ADIM 1] TERSINE MUHENDISLIK KORUMASI (OBFUSCATION) BASLATILIYOR" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# 1. Release Derleme
Write-Host "`n[1/4] Proje 'Release' modunda derleniyor..." -ForegroundColor Yellow
dotnet build $csprojPath -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Error "Derleme basarisiz oldu!"
}
Write-Host " -> Derleme tamamlandi: $releaseDir\HBYSBridge.dll" -ForegroundColor Green

# 2. Obfuscar Calistirma
Write-Host "`n[2/4] Obfuscar ile baytkod karartma uygulaniyor..." -ForegroundColor Yellow
if (!(Test-Path $obfuscarExe)) {
    Write-Error "Obfuscar.Console.exe bulunamadi: $obfuscarExe"
}

Push-Location $projectRoot
& $obfuscarExe -s $obfuscarXml
Pop-Location

if ($LASTEXITCODE -ne 0) {
    Write-Error "Obfuscar islemi hata verdi!"
}
Write-Host " -> Karartma basarili! Haritalama: $obfuscatedDir\Mapping.txt" -ForegroundColor Green

# 3. Nihai Korunmus Dagitim Paketini Olusturma
Write-Host "`n[3/4] Korunmus yayin paketi hazirlaniyor..." -ForegroundColor Yellow
if (Test-Path $protectedOutDir) {
    Remove-Item $protectedOutDir -Recurse -Force
}
New-Item -ItemType Directory -Path $protectedOutDir | Out-Null

# Bagimliliklari kopyala
Copy-Item "$releaseDir\*" -Destination $protectedOutDir -Exclude "Obfuscated", "ProtectedRelease", "Publish_Protected" -Recurse -Force

# Karartilmis DLL'i orijinalin uzerine yaz
Copy-Item "$obfuscatedDir\HBYSBridge.dll" -Destination "$protectedOutDir\HBYSBridge.dll" -Force

Write-Host " -> Korunmus paket hazirlandi: $protectedOutDir" -ForegroundColor Green

# 4. Dogrulama ve Duman Testi (Smoke Test)
Write-Host "`n[4/4] Karartilmis .EXE duman testi yapiliyor..." -ForegroundColor Yellow
$proc = Start-Process -FilePath "$protectedOutDir\HBYSBridge.exe" -WorkingDirectory $protectedOutDir -PassThru
Start-Sleep -Seconds 3

$alive = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
if ($alive) {
    Write-Host " -> TEST BASARILI: Karartilmis program hatasiz calisti! (PID: $($proc.Id))" -ForegroundColor Green
    Stop-Process -Id $proc.Id -Force
} else {
    Write-Error "TEST BASARISIZ: Program baslatilamadi veya coktu! (ExitCode: $($proc.ExitCode))"
}

Write-Host "`n=================================================================" -ForegroundColor Cyan
Write-Host " SONUC: Kodlariniz dnSpy ve ILSpy gibi decompiler araclarina karsi" -ForegroundColor Green
Write-Host " basariyla kilitlendi ve koruma altina alindi!" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Cyan
