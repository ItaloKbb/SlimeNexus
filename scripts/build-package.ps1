# SlimeNexus Build & Package Script
# This script builds and packages SlimeNexus using Velopack
# Run this locally before pushing a release

param(
    [Parameter(Mandatory=$false)]
    [string]$Version = "1.0.0",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild,
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipTest,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputDir = "./releases"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SlimeNexus Build & Package Script    " -ForegroundColor Cyan
Write-Host "  Version: $Version                    " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Ensure we're in the solution root
$solutionRoot = Split-Path -Parent $PSScriptRoot
Push-Location $solutionRoot

try {
    # Step 1: Restore & Build
    if (-not $SkipBuild) {
        Write-Host "Step 1: Building solution..." -ForegroundColor Yellow
        dotnet restore SlimeNexus.sln
        dotnet build SlimeNexus.sln --configuration Release
        
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed!"
        }
        Write-Host "Build successful!" -ForegroundColor Green
        Write-Host ""
    }

    # Step 2: Run Tests
    if (-not $SkipTest) {
        Write-Host "Step 2: Running tests..." -ForegroundColor Yellow
        dotnet test SlimeNexus.sln --configuration Release --no-build
        
        if ($LASTEXITCODE -ne 0) {
            throw "Tests failed!"
        }
        Write-Host "Tests passed!" -ForegroundColor Green
        Write-Host ""
    }

    # Step 3: Publish
    Write-Host "Step 3: Publishing application..." -ForegroundColor Yellow
    $publishDir = "./publish"
    
    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }
    
    dotnet publish src/SlimeNexus.UI/SlimeNexus.UI.csproj `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output $publishDir `
        /p:Version=$Version
    
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed!"
    }
    Write-Host "Publish successful!" -ForegroundColor Green
    Write-Host ""

    # Step 4: Check for Velopack CLI
    Write-Host "Step 4: Checking Velopack CLI..." -ForegroundColor Yellow
    $vpk = Get-Command vpk -ErrorAction SilentlyContinue
    
    if (-not $vpk) {
        Write-Host "Installing Velopack CLI..." -ForegroundColor Yellow
        dotnet tool install -g vpk
        
        # Refresh PATH
        $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
    }
    
    Write-Host "Velopack CLI ready!" -ForegroundColor Green
    Write-Host ""

    # Step 5: Create package
    Write-Host "Step 5: Creating Velopack package..." -ForegroundColor Yellow
    
    if (-not (Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir | Out-Null
    }
    
    # Check for icon
    $iconPath = "src/SlimeNexus.UI/Assets/slime-icon.ico"
    $iconArg = ""
    if (Test-Path $iconPath) {
        $iconArg = "--icon `"$iconPath`""
    } else {
        Write-Host "Warning: Icon file not found at $iconPath" -ForegroundColor Yellow
        Write-Host "The package will be created without a custom icon." -ForegroundColor Yellow
    }
    
    # Build vpk command
    $vpkCommand = @(
        "pack",
        "--packId", "SlimeNexus",
        "--packVersion", $Version,
        "--packDir", $publishDir,
        "--mainExe", "SlimeNexus.exe",
        "--packTitle", "SlimeNexus",
        "--packAuthors", "ItaloKbb",
        "--outputDir", $OutputDir
    )
    
    if (Test-Path $iconPath) {
        $vpkCommand += "--icon"
        $vpkCommand += $iconPath
    }
    
    # Check for previous releases for delta generation
    $previousReleases = Get-ChildItem -Path $OutputDir -Filter "*.nupkg" -ErrorAction SilentlyContinue
    if ($previousReleases) {
        Write-Host "Found previous releases, generating delta packages..." -ForegroundColor Yellow
        $vpkCommand += "--delta"
        $vpkCommand += $OutputDir
    }
    
    Write-Host "Running: vpk $($vpkCommand -join ' ')" -ForegroundColor Gray
    & vpk @vpkCommand
    
    if ($LASTEXITCODE -ne 0) {
        throw "Velopack packaging failed!"
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Package created successfully!        " -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Output files:" -ForegroundColor Cyan
    Get-ChildItem $OutputDir | ForEach-Object {
        Write-Host "  - $($_.Name)" -ForegroundColor White
    }
    Write-Host ""
    Write-Host "To install locally, run:" -ForegroundColor Yellow
    Write-Host "  $OutputDir\SlimeNexus-$Version-win-x64-Setup.exe" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To create a GitHub release, push a tag:" -ForegroundColor Yellow
    Write-Host "  git tag v$Version" -ForegroundColor Cyan
    Write-Host "  git push origin v$Version" -ForegroundColor Cyan

} finally {
    Pop-Location
}
