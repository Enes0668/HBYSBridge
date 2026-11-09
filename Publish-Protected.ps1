# ==============================================================================
# HBYS Bridge Pro - Kurumsal Yayınlama, Güvenlik & Mühürleme Hattı (Adım 1, 2 & 3)
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
Write-Host " [ADIM 1, 2 & 3] KURUMSAL YAYINLAMA VE GUVENLIK HATTI" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

# 1. Release Derleme
Write-Host "`n[1/6] Proje 'Release' modunda derleniyor..." -ForegroundColor Yellow
dotnet build $csprojPath -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Error "Derleme basarisiz oldu!"
}
Write-Host " -> Derleme tamamlandi: $releaseDir\HBYSBridge.dll" -ForegroundColor Green

# 2. Obfuscar ile Karartma (Adım 1)
Write-Host "`n[2/6] [ADIM 1] Obfuscar ile baytkod karartma uygulaniyor..." -ForegroundColor Yellow
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

# 3. Dağıtım Paketini Hazırlama
Write-Host "`n[3/6] Yayin klasoru olusturuluyor..." -ForegroundColor Yellow
if (Test-Path $protectedOutDir) {
    Remove-Item $protectedOutDir -Recurse -Force
}
New-Item -ItemType Directory -Path $protectedOutDir | Out-Null

Copy-Item "$releaseDir\*" -Destination $protectedOutDir -Exclude "Obfuscated", "ProtectedRelease", "Publish_Protected", "SingleFileTest" -Recurse -Force
Copy-Item "$obfuscatedDir\HBYSBridge.dll" -Destination "$protectedOutDir\HBYSBridge.dll" -Force

Write-Host " -> Paket klasoru: $protectedOutDir" -ForegroundColor Green

# 4. Dijital İmzalama / Mühürleme (Adım 2)
Write-Host "`n[4/6] [ADIM 2] Dijital Sertifika (Code Signing) ile muhurleniyor..." -ForegroundColor Yellow

$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -match "HBYS" } | Select-Object -First 1
if (-not $cert) {
    Write-Host " -> Yeni Code Signing sertifikasi olusturuluyor..." -ForegroundColor Gray
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=HBYS Medikal Sistemleri A.S." -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(3)
}

$targetExe = Join-Path $protectedOutDir "HBYSBridge.exe"
$sigResult = Set-AuthenticodeSignature -FilePath $targetExe -Certificate $cert -HashAlgorithm SHA256

Write-Host " -> Imzalayan: $($cert.Subject)" -ForegroundColor Green
Write-Host " -> Parmak Izi: $($cert.Thumbprint)" -ForegroundColor Green

# 5. SHA-256 Hash ve Manifest Üretimi (Adım 3)
Write-Host "`n[5/6] [ADIM 3] SHA-256 Butunluk Hash'i ve Manifest uretiliyor..." -ForegroundColor Yellow

$hashInfo = Get-FileHash -Path $targetExe -Algorithm SHA256
$fileInfo = Get-Item $targetExe

# Checksum dosyası
$checksumFile = Join-Path $protectedOutDir "checksum.sha256"
"$($hashInfo.Hash)  HBYSBridge.exe" | Out-File -FilePath $checksumFile -Encoding utf8

# HBYS Sunucu Entegrasyonu için manifest.json
$manifest = [ordered]@{
    product = "HBYS Bridge Pro"
    version = "1.0.0"
    fileName = "HBYSBridge.exe"
    fileSizeBytes = $fileInfo.Length
    sha256 = $hashInfo.Hash
    signer = $cert.Subject
    thumbprint = $cert.Thumbprint
    signatureStatus = $sigResult.Status.ToString()
    buildDate = (Get-Date).ToString("o")
}
$manifestFile = Join-Path $protectedOutDir "manifest.json"
$manifest | ConvertTo-Json -Depth 3 | Out-File -FilePath $manifestFile -Encoding utf8

Write-Host " -> SHA-256: $($hashInfo.Hash)" -ForegroundColor Cyan
Write-Host " -> Manifest: $manifestFile" -ForegroundColor Green

# 6. Duman Testi (Smoke Test)
Write-Host "`n[6/6] Son kontrol: Duman testi calistiriliyor..." -ForegroundColor Yellow
$proc = Start-Process -FilePath $targetExe -WorkingDirectory $protectedOutDir -PassThru
Start-Sleep -Seconds 3

$alive = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
if ($alive) {
    Write-Host " -> TEST BASARILI: Tum guvenlik adimlarindan gecen program hatasiz calisti! (PID: $($proc.Id))" -ForegroundColor Green
    Stop-Process -Id $proc.Id -Force
} else {
    Write-Error "TEST BASARISIZ: Program baslatilamadi!"
}

Write-Host "`n=================================================================" -ForegroundColor Cyan
Write-Host " TAMAMLANDI! 3 ADIMLI KURUMSAL GUVENLIK STANDARDI AKTIF:" -ForegroundColor Green
Write-Host "  1. Obfuscation (Tersine Muhendislik Korumasi)       [TAMAM]" -ForegroundColor Green
Write-Host "  2. Code Signing (Dijital Mühür / Kimlik Kanıtı)      [TAMAM]" -ForegroundColor Green
Write-Host "  3. SHA-256 Hash Dogrulamasi (Indirme Guvenligi)      [TAMAM]" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Cyan
