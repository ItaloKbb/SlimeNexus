# SlimeNexus Build & Distribution Scripts

This folder contains scripts for building, packaging, and distributing SlimeNexus.

## Quick Start

### For Developers

```powershell
# Build and create installer package
.\scripts\build-package.ps1 -Version "1.0.0"

# Build without tests (faster)
.\scripts\build-package.ps1 -Version "1.0.0" -SkipTest
```

### For Users

```powershell
# Install all dependencies (Ollama, AI model, etc.)
.\scripts\install-dependencies.ps1

# Silent install (no prompts)
.\scripts\install-dependencies.ps1 -Silent

# Skip Ollama installation
.\scripts\install-dependencies.ps1 -SkipOllama
```

---

## Scripts Overview

### `build-package.ps1`

Builds and packages SlimeNexus using Velopack.

**Parameters:**
| Parameter | Default | Description |
|-----------|---------|-------------|
| `-Version` | `1.0.0` | Version number for the package |
| `-SkipBuild` | `false` | Skip the build step |
| `-SkipTest` | `false` | Skip running tests |
| `-OutputDir` | `./releases` | Output directory for packages |

**Output Files:**
- `SlimeNexus-{version}-win-x64-Setup.exe` - Full installer
- `SlimeNexus-{version}-full.nupkg` - Full update package
- `SlimeNexus-{version}-delta.nupkg` - Delta update (if previous versions exist)
- `RELEASES` - Update manifest file

### `install-dependencies.ps1`

Installs all runtime dependencies required by SlimeNexus.

**Parameters:**
| Parameter | Default | Description |
|-----------|---------|-------------|
| `-Silent` | `false` | Run without user prompts |
| `-SkipOllama` | `false` | Skip Ollama installation |
| `-SkipDotNet` | `false` | Skip .NET runtime installation |
| `-OllamaModel` | `llama3:8b-instruct-q4_K_M` | AI model to download |

### `convert-icon.ps1`

Converts the SVG icon to ICO format for the installer.

**Requirements:**
- ImageMagick (optional, will use placeholder if not available)

---

## Velopack Commands Reference

### Manual Package Creation

```powershell
# Install Velopack CLI
dotnet tool install -g vpk

# Publish the application
dotnet publish src/SlimeNexus.UI/SlimeNexus.UI.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output ./publish

# Create the package
vpk pack `
    --packId SlimeNexus `
    --packVersion 1.0.0 `
    --packDir ./publish `
    --mainExe SlimeNexus.exe `
    --packTitle "SlimeNexus" `
    --packAuthors "ItaloKbb" `
    --icon src/SlimeNexus.UI/Assets/slime-icon.ico `
    --outputDir ./releases

# With delta updates (requires previous releases)
vpk pack `
    --packId SlimeNexus `
    --packVersion 1.1.0 `
    --packDir ./publish `
    --mainExe SlimeNexus.exe `
    --delta ./releases `
    --outputDir ./releases
```

---

## CI/CD Pipeline

The GitHub Actions workflow (`.github/workflows/ci-cd.yml`) automatically:

1. **On every push/PR:**
   - Builds the solution
   - Runs all tests

2. **On version tags (`v*`):**
   - Creates Velopack packages
   - Generates delta updates
   - Creates GitHub Release
   - Uploads installer and update packages

### Creating a Release

```bash
# Create and push a version tag
git tag v1.0.0
git push origin v1.0.0

# Or with a message
git tag -a v1.0.0 -m "Release version 1.0.0"
git push origin v1.0.0
```

---

## Update System

SlimeNexus uses Velopack for automatic updates:

1. **Check for Updates**: On startup, the app checks GitHub releases
2. **Download**: Updates are downloaded in the background
3. **Apply**: Updates are applied on next restart

The update source is configured in `UpdateManagerService.cs`:
```csharp
private const string GitHubRepo = "ItaloKbb/SlimeNexus";
```

---

## Requirements

### Build Requirements
- .NET 9 SDK
- Windows 10/11
- PowerShell 5.1+

### Runtime Requirements
- Windows 10/11 (x64)
- [Ollama](https://ollama.ai) - for local AI inference
- ~8GB RAM recommended (for AI models)
- ~10GB disk space (for AI models)
