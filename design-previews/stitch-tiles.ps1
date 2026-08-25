param(
    [Parameter(Mandatory = $true)]
    [string]$Prefix,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath
)

Add-Type -AssemblyName System.Drawing

$scale = 1
$cssWidth = 1200
$cssHeight = 2608
$scrollPositions = @(0, 720, 1440, 1888)
$pixelWidth = [int]($cssWidth * $scale)
$pixelHeight = [int]($cssHeight * $scale)

$canvas = [System.Drawing.Bitmap]::new($pixelWidth, $pixelHeight)
$canvasGraphics = [System.Drawing.Graphics]::FromImage($canvas)
$canvasGraphics.Clear([System.Drawing.Color]::Black)
$canvasGraphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy

try {
    for ($index = 0; $index -lt $scrollPositions.Count; $index++) {
        $tilePath = Join-Path $PSScriptRoot ("{0}-{1}.png" -f $Prefix, $index)
        $tile = [System.Drawing.Bitmap]::FromFile($tilePath)
        try {
            $destinationY = [int]($scrollPositions[$index] * $scale)
            $copyHeight = [Math]::Min($tile.Height, $pixelHeight - $destinationY)
            $source = [System.Drawing.Rectangle]::new(0, 0, $pixelWidth, $copyHeight)
            $destination = [System.Drawing.Rectangle]::new(0, $destinationY, $pixelWidth, $copyHeight)
            $canvasGraphics.DrawImage($tile, $destination, $source, [System.Drawing.GraphicsUnit]::Pixel)
        }
        finally {
            $tile.Dispose()
        }
    }
}
finally {
    $canvasGraphics.Dispose()
}

$final = [System.Drawing.Bitmap]::new($cssWidth, $cssHeight)
$finalGraphics = [System.Drawing.Graphics]::FromImage($final)
try {
    $finalGraphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $finalGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $finalGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $finalGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $finalGraphics.DrawImage($canvas, 0, 0, $cssWidth, $cssHeight)
    $absoluteOutput = [System.IO.Path]::GetFullPath($OutputPath)
    $final.Save($absoluteOutput, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $finalGraphics.Dispose()
    $final.Dispose()
    $canvas.Dispose()
}
