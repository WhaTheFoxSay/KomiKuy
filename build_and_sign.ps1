# ==============================================================================
# Komikuy (MangaPlus & Komiku) - Build & Signing Script for Windows 10 Mobile (ARM32)
# ==============================================================================

param(
    [string]$CertPassword = $env:KOMIKUY_CERT_PASSWORD
)

if ([string]::IsNullOrWhiteSpace($CertPassword)) {
    $CertPassword = "LumiTeamsAya2026"
}

$ProjectDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ProjectDir)) { $ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path }
$CsprojPath = Join-Path $ProjectDir "src\Mangaplus.csproj"
$BuildsDir  = Join-Path $ProjectDir "builds"

if (!(Test-Path $BuildsDir)) {
    New-Item -ItemType Directory -Force -Path $BuildsDir | Out-Null
}

$CertPath     = "$BuildsDir\Aya.cer"
$PfxPath      = "$BuildsDir\Aya.pfx"

# Ensure developer certificate exists (auto-generate if missing)
if (!(Test-Path $PfxPath)) {
    $existingPfx = Join-Path $ProjectDir "..\Lumi Teams\builds\Aya.pfx"
    if (Test-Path $existingPfx) {
        Copy-Item $existingPfx $PfxPath -Force
        if (!(Test-Path $CertPath)) {
            Copy-Item (Join-Path $ProjectDir "..\Lumi Teams\builds\Aya.cer") $CertPath -Force
        }
    } else {
        Write-Host "Creating self-signed developer certificate (CN=Aya)..." -ForegroundColor Yellow
        $cert = New-SelfSignedCertificate -Type Custom -Subject "CN=Aya" `
            -KeyUsage DigitalSignature -FriendlyName "KomiKuy Developer Certificate" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")
        $securePwd = ConvertTo-SecureString -String $CertPassword -Force -AsPlainText
        Export-PfxCertificate -Cert $cert -FilePath $PfxPath -Password $securePwd | Out-Null
        if (!(Test-Path $CertPath)) {
            Export-Certificate -Cert $cert -FilePath $CertPath -Type CERT | Out-Null
        }
    }
}

$VsDevCmd = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat"
if (!(Test-Path $VsDevCmd)) {
    $VsDevCmd = "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\Tools\VsDevCmd.bat"
}

$SigntoolPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe"
if (!(Test-Path $SigntoolPath)) {
    $SigntoolPath = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe"
}

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  KOMIKUY / MANGAPLUS - WINDOWS 10 MOBILE (ARM32)       " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# 1. Restore NuGet Packages
if (-not (Test-Path "$ProjectDir\src\obj\project.assets.json")) {
    Write-Host "[1/4] Restoring NuGet packages..." -ForegroundColor Yellow
    cmd.exe /c "call `"$VsDevCmd`" && msbuild `"$CsprojPath`" /t:Restore /p:RestoreIgnoreFailedSources=true < NUL" | Out-Null
} else {
    Write-Host "[1/4] NuGet packages already restored, skipping restore..." -ForegroundColor Green
}

# 2. Clean previous build artifacts
Write-Host "[2/4] Cleaning previous build outputs..." -ForegroundColor Yellow
Get-ChildItem -Path $BuildsDir -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

if (Test-Path "$ProjectDir\src\bin\ARM\Release") {
    Remove-Item -Path "$ProjectDir\src\bin\ARM\Release" -Recurse -Force -ErrorAction SilentlyContinue
}
if (Test-Path "$ProjectDir\src\obj\ARM\Release") {
    Remove-Item -Path "$ProjectDir\src\obj\ARM\Release" -Recurse -Force -ErrorAction SilentlyContinue
}

# 3. Build Solution
Write-Host "[3/4] Compiling Komikuy (ARM32 Release)..." -ForegroundColor Yellow
$cmdScript = @"
@echo off
call "$VsDevCmd"
msbuild "$CsprojPath" /t:Rebuild /p:Configuration=Release /p:Platform=ARM /p:GenerateAppxPackageOnBuild=true "/p:AppxPackageDir=$BuildsDir\\" /p:AppxBundle=Never /p:UapAppxPackageBuildMode=Direct /p:AppxPackageSigningEnabled=false /p:TargetPlatformVersion=10.0.19041.0 /p:TargetPlatformMinVersion=10.0.10586.0 /nr:false /v:minimal < NUL
"@
$cmdFile = Join-Path $ProjectDir "temp_build.cmd"
[System.IO.File]::WriteAllText($cmdFile, $cmdScript)
& cmd.exe /c "$cmdFile"
$buildExitCode = $LASTEXITCODE
Remove-Item $cmdFile -Force -ErrorAction SilentlyContinue

if ($buildExitCode -ne 0) {
    Write-Host "[ERROR] Compilation failed with exit code $buildExitCode. Aborting build." -ForegroundColor Red
    exit $buildExitCode
}

# 3. Locate Most Recent Generated APPX in Subfolders
Write-Host "[3/4] Locating generated APPX..." -ForegroundColor Yellow
$appxFiles = Get-ChildItem -Path $BuildsDir -Filter "*.appx" -Recurse | Where-Object { $_.FullName -notmatch "Mangaplus_v" -and $_.FullName -notmatch "KomiKuy_v" } | Sort-Object LastWriteTime -Descending

if ($appxFiles -and $appxFiles.Count -gt 0) {
    $targetAppx = $appxFiles[0].FullName
    $finalVersionAppx = "$BuildsDir\KomiKuy_v1.0.0_ARM.appx"
    $legacyVersionAppx = "$BuildsDir\Mangaplus_v1.0.0_ARM.appx"
    
    # 4. Sign Package
    Write-Host "[4/4] Signing APPX package ($targetAppx) with Signtool..." -ForegroundColor Yellow
    & $SigntoolPath sign /fd SHA256 /a /f $PfxPath /p $CertPassword $targetAppx
    
    Copy-Item $targetAppx $finalVersionAppx -Force
    Copy-Item $targetAppx $legacyVersionAppx -Force
    
    Write-Host ""
    Write-Host "========================================================" -ForegroundColor Green
    Write-Host "[SUCCESS] Komikuy v1.0.0 created & signed successfully!" -ForegroundColor Green
    Write-Host "APPX: $finalVersionAppx" -ForegroundColor Green
    Write-Host "Cert: $CertPath" -ForegroundColor Green
    Write-Host "========================================================" -ForegroundColor Green
} else {
    Write-Host "[ERROR] APPX package not found in $BuildsDir" -ForegroundColor Red
    exit 1
}
