# ==============================================================================
# FLOW — Authenticode Code Signing Script
# Signs executables, DLLs, and installers with SHA-256 and RFC 3161 timestamps
# ==============================================================================
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string[]]$FilesToSign,

    [string]$PfxPath = "",
    [string]$PfxPassword = "FlowDevSecure2026!",
    [string]$TimestampServer = "http://timestamp.digicert.com",
    [switch]$VerifyOnly
)

$ErrorActionPreference = "Stop"
Write-Host "=== FLOW Authenticode Code Signing Tool ===" -ForegroundColor Cyan

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ScriptDir) { $ScriptDir = $PSScriptRoot }
$RepoRoot = (Get-Item "$ScriptDir\..\..").FullName

if (-not $PfxPath) {
    $PfxPath = Join-Path $RepoRoot "artifacts\certs\FLOW-Dev-Certificate.pfx"
}

# Locate signtool.exe
$signtoolPaths = @(
    (Get-Command signtool.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    (Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue | Select-Object -Last 1 -ExpandProperty FullName),
    (Get-ChildItem "C:\Program Files\Microsoft Visual Studio\*\*\SDK\ScopeCppSDK\vc15\SDK\bin\signtool.exe" -ErrorAction SilentlyContinue | Select-Object -Last 1 -ExpandProperty FullName)
) | Where-Object { $_ -and (Test-Path $_) }

$signtool = $signtoolPaths | Select-Object -First 1

if (-not $signtool) {
    Write-Warning "signtool.exe not found in standard Windows SDK paths. Checking Set-AuthenticodeSignature..."
}

foreach ($target in $FilesToSign) {
    if (-not (Test-Path $target)) {
        Write-Warning "File not found: $target"
        continue
    }

    if ($VerifyOnly) {
        $sig = Get-AuthenticodeSignature $target
        Write-Host "File: $target - Status: $($sig.Status) - Signer: $($sig.SignerCertificate.Subject)" -ForegroundColor Cyan
        continue
    }

    Write-Host "Signing: $target..." -ForegroundColor Yellow

    if ($signtool -and (Test-Path $PfxPath)) {
        & $signtool sign /f $PfxPath /p $PfxPassword /fd SHA256 /tr $TimestampServer /td SHA256 /v $target
        if ($LASTEXITCODE -ne 0) {
            # Try without timestamp if network / timestamp server unreachable
            Write-Warning "Timestamping failed. Retrying sign without timestamp..."
            & $signtool sign /f $PfxPath /p $PfxPassword /fd SHA256 /v $target
        }
    } elseif (Test-Path $PfxPath) {
        # Fallback to PowerShell Set-AuthenticodeSignature
        $securePassword = ConvertTo-SecureString -String $PfxPassword -Force -AsPlainText
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($PfxPath, $securePassword)
        $sig = Set-AuthenticodeSignature -FilePath $target -Certificate $cert -HashAlgorithm SHA256 -TimestampServer $TimestampServer
        if ($sig.Status -ne "Valid") {
            Set-AuthenticodeSignature -FilePath $target -Certificate $cert -HashAlgorithm SHA256
        }
    } else {
        Write-Warning "No certificate found at $PfxPath. Run Create-DevCertificate.ps1 first to generate one."
        continue
    }

    $verified = Get-AuthenticodeSignature $target
    Write-Host "Signed: $target -> Status: $($verified.Status)" -ForegroundColor Green
}

Write-Host "Signing operation complete." -ForegroundColor Cyan
