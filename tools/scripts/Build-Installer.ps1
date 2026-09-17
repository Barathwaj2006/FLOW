# ==============================================================================
# FLOW — Master Installer Build & Packaging Pipeline
# Builds self-contained binaries, bundles web assets, and generates installers
# ==============================================================================
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "1.0.0",
    [switch]$SkipWebBuild,
    [switch]$SignBinaries,
    [switch]$InstallPrerequisites,
    [switch]$VelopackPack
)

$ErrorActionPreference = "Stop"
$sw = [System.Diagnostics.Stopwatch]::StartNew()

$RepoRoot = Resolve-Path "$PSScriptRoot\..\.."
$PublishDir = Join-Path $RepoRoot "artifacts\publish\$Runtime"
$InstallerDir = Join-Path $RepoRoot "artifacts\installer"
$ReleaseDir = Join-Path $RepoRoot "artifacts\release"
$DistDir = Join-Path $RepoRoot "dist"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "FLOW — Automated Windows Installer & Package Builder" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration | Runtime: $Runtime" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# Ensure clean target directories
@($PublishDir, $InstallerDir, $ReleaseDir) | ForEach-Object {
    if (-not (Test-Path $_)) {
        New-Item -ItemType Directory -Path $_ -Force | Out-Null
    }
}

# ------------------------------------------------------------------------------
# STEP 1: Build Web UI Assets
# ------------------------------------------------------------------------------
if (-not $SkipWebBuild) {
    Write-Host "`n[1/6] Building React production bundle (Vite)..." -ForegroundColor Yellow
    Push-Location $RepoRoot
    try {
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw "Web build failed with exit code $LASTEXITCODE" }
    } finally {
        Pop-Location
    }
    Write-Host "React assets compiled into $DistDir." -ForegroundColor Green
} else {
    Write-Host "`n[1/6] Skipping Web UI build (-SkipWebBuild specified)." -ForegroundColor DarkGray
}

# ------------------------------------------------------------------------------
# STEP 2: Publish .NET 9 Self-Contained Host with uiAccess="true"
# ------------------------------------------------------------------------------
Write-Host "`n[2/6] Publishing Flow.Host.Windows (Self-Contained x64 with EnableUiAccess=true)..." -ForegroundColor Yellow
$projectFile = Join-Path $RepoRoot "src\Flow.Host.Windows\Flow.Host.Windows.csproj"

& dotnet publish $projectFile `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:EnableUiAccess=true `
    -p:PublishSingleFile=false `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}
Write-Host "Host published successfully to: $PublishDir" -ForegroundColor Green

# ------------------------------------------------------------------------------
# STEP 3: Bundle Web Assets & Licenses into Publish Folder
# ------------------------------------------------------------------------------
Write-Host "`n[3/6] Bundling Web Assets, Documentation, and Licenses..." -ForegroundColor Yellow
$targetDist = Join-Path $PublishDir "dist"
if (Test-Path $DistDir) {
    Copy-Item -Path $DistDir -Destination $PublishDir -Recurse -Force
    Write-Host "Copied React dist/ into publish bundle." -ForegroundColor Green
}

$licenseFile = Join-Path $RepoRoot "LICENSE"
if (Test-Path $licenseFile) {
    Copy-Item -Path $licenseFile -Destination $PublishDir -Force
}

# ------------------------------------------------------------------------------
# STEP 4: Code Signing (Optional / Dev Certificate)
# ------------------------------------------------------------------------------
if ($SignBinaries) {
    Write-Host "`n[4/6] Code Signing Executables and Assemblies..." -ForegroundColor Yellow
    $signScript = Join-Path $PSScriptRoot "Sign-Package.ps1"
    $certScript = Join-Path $PSScriptRoot "Create-DevCertificate.ps1"
    $devCert = Join-Path $RepoRoot "artifacts\certs\FLOW-Dev-Certificate.pfx"

    if (-not (Test-Path $devCert)) {
        Write-Host "Dev certificate not found. Generating self-signed certificate..." -ForegroundColor Cyan
        & powershell -ExecutionPolicy Bypass -File $certScript
    }

    $exeToSign = Join-Path $PublishDir "Flow.Host.Windows.exe"
    & powershell -ExecutionPolicy Bypass -File $signScript -FilesToSign $exeToSign
} else {
    Write-Host "`n[4/6] Code Signing skipped (specify -SignBinaries to enable)." -ForegroundColor DarkGray
}

# ------------------------------------------------------------------------------
# STEP 5: Compile Inno Setup 6 Installer
# ------------------------------------------------------------------------------
Write-Host "`n[5/6] Checking for Inno Setup 6 Compiler (ISCC)..." -ForegroundColor Yellow

$isccPaths = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) }

$iscc = $isccPaths | Select-Object -First 1

if (-not $iscc -and $InstallPrerequisites) {
    Write-Host "Inno Setup not found. Installing via winget..." -ForegroundColor Cyan
    & winget install JRSoftware.InnoSetup -e --silent --accept-package-agreements --accept-source-agreements
    $iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
}

if ($iscc -and (Test-Path $iscc)) {
    Write-Host "Compiling Inno Setup package using $iscc..." -ForegroundColor Cyan
    $issFile = Join-Path $RepoRoot "installer\FLOW.iss"
    & $iscc $issFile
    if ($LASTEXITCODE -eq 0) {
        $setupExe = Join-Path $InstallerDir "FLOW-Setup-v1.0.0-win-x64.exe"
        if (Test-Path $setupExe) {
            Write-Host "Inno Setup installer created: $setupExe" -ForegroundColor Green
            if ($SignBinaries) {
                & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "Sign-Package.ps1") -FilesToSign $setupExe
            }
        }
    } else {
        Write-Warning "Inno Setup compilation failed with code $LASTEXITCODE."
    }
} else {
    Write-Host "Inno Setup compiler (ISCC.exe) not found on system." -ForegroundColor Yellow
    Write-Host "Install Inno Setup 6 (winget install JRSoftware.InnoSetup) to compile FLOW-Setup-v1.0.0-win-x64.exe." -ForegroundColor DarkGray
}

# ------------------------------------------------------------------------------
# STEP 6: Package Portable Release Archive
# ------------------------------------------------------------------------------
Write-Host "`n[6/6] Packaging Standalone Portable Zip Archive..." -ForegroundColor Yellow
$zipArchive = Join-Path $ReleaseDir "FLOW-v1.0.0-win-x64.zip"
if (Test-Path $zipArchive) { Remove-Item $zipArchive -Force }

Compress-Archive -Path "$PublishDir\*" -DestinationPath $zipArchive -CompressionLevel Optimal
$zipItem = Get-Item $zipArchive
$zipSizeMB = [math]::Round($zipItem.Length / 1MB, 2)
$zipHash = (Get-FileHash $zipArchive -Algorithm SHA256).Hash

Write-Host "Portable archive created: $zipArchive ($zipSizeMB MB)" -ForegroundColor Green
Write-Host "SHA256: $zipHash" -ForegroundColor Cyan

# ------------------------------------------------------------------------------
# STEP 7: Package Velopack Delta Update Bundle (Optional)
# ------------------------------------------------------------------------------
$vpkOutDir = Join-Path $ReleaseDir "velopack"
if ($VelopackPack) {
    Write-Host "`n[7/7] Packaging Velopack Delta Update Release (vpk)..." -ForegroundColor Yellow
    $vpkCmd = Get-Command vpk -ErrorAction SilentlyContinue
    if (-not $vpkCmd -and $InstallPrerequisites) {
        Write-Host "Installing Velopack CLI (vpk) dotnet tool..." -ForegroundColor Cyan
        & dotnet tool install -g vpk
        $vpkCmd = Get-Command vpk -ErrorAction SilentlyContinue
    }

    if ($vpkCmd) {
        if (-not (Test-Path $vpkOutDir)) { New-Item -ItemType Directory -Path $vpkOutDir -Force | Out-Null }
        Write-Host "Running: vpk pack -u FLOW -v $Version -p $PublishDir -e Flow.Host.Windows.exe -o $vpkOutDir" -ForegroundColor Cyan
        & vpk pack -u FLOW -v $Version -p $PublishDir -e Flow.Host.Windows.exe -o $vpkOutDir
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Velopack release package generated successfully in: $vpkOutDir" -ForegroundColor Green
        } else {
            Write-Warning "Velopack pack returned exit code $LASTEXITCODE."
        }
    } else {
        Write-Host "Velopack CLI (vpk) is not installed." -ForegroundColor Yellow
        Write-Host "Install it with: dotnet tool install -g vpk" -ForegroundColor DarkGray
    }
}

$sw.Stop()
Write-Host "`n============================================================" -ForegroundColor Green
Write-Host "Build & Packaging Completed Successfully in $($sw.Elapsed.ToString('mm\:ss'))!" -ForegroundColor Green
Write-Host "Publish Folder: $PublishDir" -ForegroundColor Cyan
Write-Host "Installer:      $InstallerDir" -ForegroundColor Cyan
Write-Host "Release Zip:    $zipArchive" -ForegroundColor Cyan
if ($VelopackPack) {
    Write-Host "Velopack Out:   $vpkOutDir" -ForegroundColor Cyan
}
Write-Host "============================================================" -ForegroundColor Green
