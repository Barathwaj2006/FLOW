# ==============================================================================
# FLOW — Create Local Development Code Signing Certificate
# Generates a self-signed Authenticode certificate for testing uiAccess="true"
# ==============================================================================
[CmdletBinding()]
param(
    [string]$CertSubject = "CN=FLOW Local Development Code Signing",
    [string]$OutputDir = "",
    [string]$Password = "FlowDevSecure2026!",
    [switch]$InstallToRoot
)

$ErrorActionPreference = "Stop"
Write-Host "=== FLOW Developer Code Signing Certificate Generator ===" -ForegroundColor Cyan

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $ScriptDir) { $ScriptDir = $PSScriptRoot }
$RepoRoot = (Get-Item "$ScriptDir\..\..").FullName

if (-not $OutputDir) {
    $OutputDir = Join-Path $RepoRoot "artifacts\certs"
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$pfxPath = Join-Path $OutputDir "FLOW-Dev-Certificate.pfx"
$cerPath = Join-Path $OutputDir "FLOW-Dev-Certificate.cer"

Write-Host "Creating self-signed certificate with Code Signing EKU (1.3.6.1.5.5.7.3.3)..." -ForegroundColor Yellow

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $CertSubject `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -KeyExportPolicy Exportable `
    -HashAlgorithm SHA256 `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears(5)

Write-Host "Certificate generated successfully. Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

# Export to PFX with password
$securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null

Write-Host "Exported PFX: $pfxPath" -ForegroundColor Green
Write-Host "Exported CER: $cerPath" -ForegroundColor Green

if ($InstallToRoot) {
    Write-Host "Installing certificate to Trusted Root and Trusted Publisher stores..." -ForegroundColor Yellow
    
    $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
    $rootStore.Open("ReadWrite")
    $rootStore.Add($cert)
    $rootStore.Close()

    $pubStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("TrustedPublisher", "CurrentUser")
    $pubStore.Open("ReadWrite")
    $pubStore.Add($cert)
    $pubStore.Close()

    Write-Host "Certificate installed into CurrentUser\Root and CurrentUser\TrustedPublisher." -ForegroundColor Green
    Write-Host "Binaries signed with this certificate can now run with uiAccess='true' from %ProgramFiles%." -ForegroundColor Cyan
} else {
    Write-Host "Run with -InstallToRoot to trust this certificate for local uiAccess='true' execution." -ForegroundColor DarkGray
}

Write-Host "Done." -ForegroundColor Cyan
