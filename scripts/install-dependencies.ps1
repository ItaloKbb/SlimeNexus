# SlimeNexus Dependency Installer
# This script installs all required dependencies for SlimeNexus to run
# Including Ollama for local AI inference

param(
    [switch]$Silent,
    [switch]$SkipOllama,
    [switch]$SkipDotNet,
    [string]$OllamaModel = "llama3:8b-instruct-q4_K_M"
)

$ErrorActionPreference = "Stop"

function Write-Banner {
    Write-Host ""
    Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║           SlimeNexus Dependency Installer                ║" -ForegroundColor Cyan
    Write-Host "║                                                           ║" -ForegroundColor Cyan
    Write-Host "║   This will install:                                     ║" -ForegroundColor Cyan
    Write-Host "║   • .NET 9 Desktop Runtime                               ║" -ForegroundColor White
    Write-Host "║   • Ollama (Local AI Runtime)                            ║" -ForegroundColor White
    Write-Host "║   • Recommended AI Model                                 ║" -ForegroundColor White
    Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}

function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Install-WithWinget {
    param(
        [string]$PackageId,
        [string]$DisplayName
    )
    
    Write-Host "Installing $DisplayName..." -ForegroundColor Yellow
    
    $result = winget install $PackageId --accept-source-agreements --accept-package-agreements 2>&1
    
    # Check for various success/already-installed scenarios
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ $DisplayName installed successfully!" -ForegroundColor Green
        return $true
    }
    elseif ($result -match "already installed") {
        Write-Host "✓ $DisplayName is already installed." -ForegroundColor Green
        return $true
    }
    else {
        Write-Host "✗ Failed to install $DisplayName" -ForegroundColor Red
        Write-Host "  Error: $result" -ForegroundColor Gray
        return $false
    }
}

function Install-OllamaModel {
    param([string]$ModelName)
    
    Write-Host ""
    Write-Host "Downloading AI model: $ModelName" -ForegroundColor Yellow
    Write-Host "This may take 5-15 minutes depending on your internet speed..." -ForegroundColor Gray
    Write-Host ""
    
    # Ensure Ollama service is running
    $ollamaProcess = Get-Process -Name "ollama" -ErrorAction SilentlyContinue
    if (-not $ollamaProcess) {
        Write-Host "Starting Ollama service..." -ForegroundColor Gray
        Start-Process "ollama" -ArgumentList "serve" -WindowStyle Hidden
        Start-Sleep -Seconds 3
    }
    
    # Pull the model
    try {
        & ollama pull $ModelName
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "✓ AI model downloaded successfully!" -ForegroundColor Green
            return $true
        }
    }
    catch {
        Write-Host "Model download failed: $_" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "You can manually download the model later by running:" -ForegroundColor Yellow
    Write-Host "  ollama pull $ModelName" -ForegroundColor Cyan
    return $false
}

# Main script
Write-Banner

# Check for admin rights
if (-not (Test-Administrator)) {
    Write-Host "⚠ Warning: Not running as Administrator" -ForegroundColor Yellow
    Write-Host "  Some installations may require elevation." -ForegroundColor Gray
    Write-Host ""
}

# Check for winget
$winget = Get-Command winget -ErrorAction SilentlyContinue
if (-not $winget) {
    Write-Host "✗ Windows Package Manager (winget) not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install winget from:" -ForegroundColor Yellow
    Write-Host "  • Microsoft Store (App Installer)" -ForegroundColor Cyan
    Write-Host "  • https://github.com/microsoft/winget-cli/releases" -ForegroundColor Cyan
    Write-Host ""
    exit 1
}

Write-Host "✓ Windows Package Manager found" -ForegroundColor Green
Write-Host ""

$success = $true

# Install .NET Runtime
if (-not $SkipDotNet) {
    if (-not (Install-WithWinget "Microsoft.DotNet.DesktopRuntime.9" ".NET 9 Desktop Runtime")) {
        Write-Host ""
        Write-Host "Note: SlimeNexus includes a self-contained runtime as fallback." -ForegroundColor Yellow
        # Don't fail the script for .NET since we're self-contained
    }
    Write-Host ""
}

# Install Ollama
if (-not $SkipOllama) {
    if (Install-WithWinget "Ollama.Ollama" "Ollama (Local AI Runtime)") {
        # Install the AI model
        Install-OllamaModel -ModelName $OllamaModel
    }
    else {
        Write-Host ""
        Write-Host "You can install Ollama manually from: https://ollama.ai" -ForegroundColor Yellow
        $success = $false
    }
}

# Summary
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
if ($success) {
    Write-Host "  Installation Complete! ✓" -ForegroundColor Green
    Write-Host ""
    Write-Host "  You can now run SlimeNexus!" -ForegroundColor White
}
else {
    Write-Host "  Installation completed with warnings" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  Some components may need manual installation." -ForegroundColor White
}
Write-Host "═══════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Quick verification
Write-Host "Installed components:" -ForegroundColor Cyan
$dotnetInstalled = Get-Command dotnet -ErrorAction SilentlyContinue
$ollamaInstalled = Get-Command ollama -ErrorAction SilentlyContinue

if ($dotnetInstalled) {
    $dotnetVersion = & dotnet --version 2>$null
    Write-Host "  ✓ .NET: $dotnetVersion" -ForegroundColor Green
}
else {
    Write-Host "  ✗ .NET: Not in PATH (self-contained fallback will be used)" -ForegroundColor Yellow
}

if ($ollamaInstalled) {
    Write-Host "  ✓ Ollama: Installed" -ForegroundColor Green
}
else {
    Write-Host "  ✗ Ollama: Not in PATH" -ForegroundColor Yellow
}

Write-Host ""

if (-not $Silent) {
    Write-Host "Press any key to exit..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
