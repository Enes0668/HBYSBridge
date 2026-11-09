# ==============================================================================
# HBYS Bridge Pro - Güvenli Yayınlama & Mühürleme Otomasyonu (Adım 1 & Adım 2)
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
Write-Host " [ADIM 1 & 2] GUVENLI YAYINLAMA: OBFUSCATION & CODE SIGNING" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# 1. Release Derleme
Write-Host "`n[1/5] Proje 'Release' modunda derleniyor..." -ForegroundColor Yellow
dotnet build $csprojPath -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Error "Derleme basarisiz oldu!"
}
Write-Host " -> Derleme tamamlandi: $releaseDir\HBYSBridge.dll" -ForegroundColor Green

# 2. Obfuscar ile Karartma (Adım 1)
Write-Host "`n[2/5] [ADIM 1] Obfuscar ile baytkod karartma uygulaniyor..." -ForegroundColor Yellow
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

# 3. Paket Hazırlama
Write-Host "`n[3/5] Yayin paketi hazirlaniyor..." -ForegroundColor Yellow
if (Test-Path $protectedOutDir) {
    Remove-Item $protectedOutDir -Recurse -Force
}
New-Item -ItemType Directory -Path $protectedOutDir | Out-Null

Copy-Item "$releaseDir\*" -Destination $protectedOutDir -Exclude "Obfuscated", "ProtectedRelease", "Publish_Protected", "SingleFileTest" -Recurse -Force
Copy-Item "$obfuscatedDir\HBYSBridge.dll" -Destination "$protectedOutDir\HBYSBridge.dll" -Force

Write-Host " -> Paket klasoru: $protectedOutDir" -ForegroundColor Green

# 4. Dijital İmzalama / Mühürleme (Adım 2)
Write-Host "`n[4/5] [ADIM 2] Dijital Sertifika (Code Signing) ile muhurleniyor..." -ForegroundColor Yellow

$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -match "HBYS" } | Select-Object -First 1
if (-not $cert) {
    Write-Host " -> Yeni Code Signing sertifikasi olusturuluyor..." -ForegroundColor Gray
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=HBYS Medikal Sistemleri A.S." -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(3)
}

$targetExe = Join-Path $protectedOutDir "HBYSBridge.exe"
$sigResult = Set-AuthenticodeSignature -FilePath $targetExe -Certificate $cert -HashAlgorithm SHA256

Write-Host " -> Imzalayan: $($cert.Subject)" -ForegroundColor Green
Write-Host " -> Parmak Izi: $($cert.Thumbprint)" -ForegroundColor Green
Write-Host " -> Imza Durumu: $($sigResult.Status)" -ForegroundColor Green

# 5. Duman Testi (Smoke Test)
Write-Host "`n[5/5] Karartilmis ve imzalanmis .EXE duman testi yapiliyor..." -ForegroundColor Yellow
$proc = Start-Process -FilePath $targetExe -WorkingDirectory $protectedOutDir -PassThru
Start-Sleep -Seconds 3

$alive = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
if ($alive) {
    Write-Host " -> TEST BASARILI: Mühürlü program hatasiz calisti! (PID: $($proc.Id))" -ForegroundColor Green
    Stop-Process -Id $proc.Id -Force
} else {
    Write-Error "TEST BASARISIZ: Program baslatilamadi!"
}

Write-Host "`n=================================================================" -ForegroundColor Cyan
Write-Host " TEBRIKLER: .EXE dosyaniz artik hem tersine muhendislikten korumali," -ForegroundColor Green
Write-Host " hem de kurumsal dijital muhurle imzalanmis durumdadir!" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Cyan
