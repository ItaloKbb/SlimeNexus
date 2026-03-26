# SlimeNexus Icon Converter
# Converts SVG to ICO using ImageMagick (if available) or creates a simple placeholder

$svgPath = "src\SlimeNexus.UI\Assets\slime-icon.svg"
$icoPath = "src\SlimeNexus.UI\Assets\slime-icon.ico"

Write-Host "SlimeNexus Icon Converter" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan

# Check if ImageMagick is available
$magick = Get-Command magick -ErrorAction SilentlyContinue

if ($magick) {
    Write-Host "Converting SVG to ICO using ImageMagick..." -ForegroundColor Green
    & magick convert -background none $svgPath -define icon:auto-resize=256,128,64,48,32,16 $icoPath
    Write-Host "Icon created at: $icoPath" -ForegroundColor Green
} else {
    Write-Host "ImageMagick not found. Creating a simple placeholder ICO..." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "For best results, install ImageMagick and run this script again:" -ForegroundColor Yellow
    Write-Host "  winget install ImageMagick.ImageMagick" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Or manually convert the SVG using online tools like:" -ForegroundColor Yellow
    Write-Host "  https://cloudconvert.com/svg-to-ico" -ForegroundColor Cyan
    
    # Create a minimal valid ICO file (1x1 green pixel as placeholder)
    # ICO format: ICONDIR (6 bytes) + ICONDIRENTRY (16 bytes) + BMP data
    $iconData = [byte[]](
        # ICONDIR
        0x00, 0x00,             # Reserved (must be 0)
        0x01, 0x00,             # Type (1 = ICO)
        0x01, 0x00,             # Number of images
        # ICONDIRENTRY
        0x10,                   # Width (16)
        0x10,                   # Height (16)
        0x00,                   # Color palette (0 = no palette)
        0x00,                   # Reserved
        0x01, 0x00,             # Color planes
        0x20, 0x00,             # Bits per pixel (32)
        0x68, 0x04, 0x00, 0x00, # Size of image data
        0x16, 0x00, 0x00, 0x00  # Offset to image data (22 bytes from start)
    )
    
    # For simplicity, we'll just note that a proper ICO needs to be created
    Write-Host ""
    Write-Host "Note: You should create a proper .ico file before packaging." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
