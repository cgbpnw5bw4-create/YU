param(
    [Parameter(Mandatory)] [string]$ReferencePath,
    [Parameter(Mandatory)] [string]$ImplementationPath,
    [Parameter(Mandatory)] [string]$OutputPath,
    [int]$PanelWidth = 512,
    [int]$PanelHeight = 320
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$reference = [System.Drawing.Bitmap]::FromFile([System.IO.Path]::GetFullPath($ReferencePath))
$implementation = [System.Drawing.Bitmap]::FromFile([System.IO.Path]::GetFullPath($ImplementationPath))
try {
    $canvas = New-Object System.Drawing.Bitmap(
        ($PanelWidth * 2),
        $PanelHeight,
        [System.Drawing.Imaging.PixelFormat]::Format32bppPArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($canvas)
        try {
            $graphics.Clear([System.Drawing.Color]::Black)
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.DrawImage($reference, (New-Object System.Drawing.Rectangle(0, 0, $PanelWidth, $PanelHeight)))
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $graphics.DrawImage($implementation, (New-Object System.Drawing.Rectangle($PanelWidth, 0, $PanelWidth, $PanelHeight)))
        }
        finally {
            $graphics.Dispose()
        }

        $resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
        [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($resolvedOutput))
        $canvas.Save($resolvedOutput, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $canvas.Dispose()
    }
}
finally {
    $reference.Dispose()
    $implementation.Dispose()
}

Write-Output "COMPARISON=$resolvedOutput SIZE=$($PanelWidth * 2)x$PanelHeight"
