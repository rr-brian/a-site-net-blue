# Script to create a favicon.ico from logo.png
Add-Type -AssemblyName System.Drawing

$sourcePath = ".\Backend\wwwroot\images\logo.png"
$outputPath = ".\Backend\wwwroot\favicon.ico"

# Check if source file exists
if (-not (Test-Path $sourcePath)) {
    Write-Error "Source file not found: $sourcePath"
    exit 1
}

try {
    # Load the source image
    $sourceImage = [System.Drawing.Image]::FromFile($sourcePath)
    
    # Create a new bitmap with favicon dimensions (16x16 is standard)
    $faviconSize = 32 # Using 32x32 for better quality
    $favicon = New-Object System.Drawing.Bitmap($faviconSize, $faviconSize)
    
    # Create graphics object from the bitmap
    $graphics = [System.Drawing.Graphics]::FromImage($favicon)
    
    # Set high quality resizing
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    
    # Draw the source image onto the favicon bitmap, resizing it
    $graphics.DrawImage($sourceImage, 0, 0, $faviconSize, $faviconSize)
    
    # Save as ICO file
    $favicon.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Icon)
    
    # Clean up
    $graphics.Dispose()
    $favicon.Dispose()
    $sourceImage.Dispose()
    
    Write-Host "Favicon created successfully at $outputPath"
}
catch {
    Write-Error "Error creating favicon: $_"
    exit 1
}
