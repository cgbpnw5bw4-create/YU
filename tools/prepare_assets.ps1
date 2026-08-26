param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function Convert-SpriteSheet {
    param(
        [Parameter(Mandatory)] [string]$SourcePath,
        [Parameter(Mandatory)] [string]$DestinationPath,
        [Parameter(Mandatory)] [int]$Columns,
        [Parameter(Mandatory)] [int]$Rows,
        [int]$CellSize = 16,
        [int]$Padding = 1
    )

    $source = [System.Drawing.Bitmap]::FromFile($SourcePath)
    try {
        $destination = New-Object System.Drawing.Bitmap(
            ($Columns * $CellSize),
            ($Rows * $CellSize),
            [System.Drawing.Imaging.PixelFormat]::Format32bppPArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($destination)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighSpeed
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None

                for ($row = 0; $row -lt $Rows; $row++) {
                    $cellTop = [int]($row * $source.Height / $Rows)
                    $cellBottom = [int](($row + 1) * $source.Height / $Rows)
                    for ($column = 0; $column -lt $Columns; $column++) {
                        $cellLeft = [int]($column * $source.Width / $Columns)
                        $cellRight = [int](($column + 1) * $source.Width / $Columns)
                        $minimumX = $cellRight
                        $minimumY = $cellBottom
                        $maximumX = $cellLeft - 1
                        $maximumY = $cellTop - 1

                        for ($y = $cellTop; $y -lt $cellBottom; $y++) {
                            for ($x = $cellLeft; $x -lt $cellRight; $x++) {
                                if ($source.GetPixel($x, $y).A -lt 24) { continue }
                                $minimumX = [Math]::Min($minimumX, $x)
                                $minimumY = [Math]::Min($minimumY, $y)
                                $maximumX = [Math]::Max($maximumX, $x)
                                $maximumY = [Math]::Max($maximumY, $y)
                            }
                        }

                        if ($maximumX -lt $minimumX) { continue }
                        $sourceRect = [System.Drawing.Rectangle]::FromLTRB(
                            $minimumX,
                            $minimumY,
                            ($maximumX + 1),
                            ($maximumY + 1))
                        $available = $CellSize - (2 * $Padding)
                        $scale = [Math]::Min(
                            ($available / [double]$sourceRect.Width),
                            ($available / [double]$sourceRect.Height))
                        $drawWidth = [Math]::Max(1, [int][Math]::Round($sourceRect.Width * $scale))
                        $drawHeight = [Math]::Max(1, [int][Math]::Round($sourceRect.Height * $scale))
                        $drawX = ($column * $CellSize) + [int](($CellSize - $drawWidth) / 2)
                        $drawY = ($row * $CellSize) + [int](($CellSize - $drawHeight) / 2)
                        $destinationRect = New-Object System.Drawing.Rectangle($drawX, $drawY, $drawWidth, $drawHeight)
                        $graphics.DrawImage(
                            $source,
                            $destinationRect,
                            $sourceRect,
                            [System.Drawing.GraphicsUnit]::Pixel)
                    }
                }

                # Keep the embedded atlas genuinely pixel-sharp. Image-generation
                # sources can contain antialiased alpha edges even when the visible
                # art direction is pixel art.
                for ($y = 0; $y -lt $destination.Height; $y++) {
                    for ($x = 0; $x -lt $destination.Width; $x++) {
                        $pixel = $destination.GetPixel($x, $y)
                        if ($pixel.A -lt 128) {
                            $destination.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
                        }
                        elseif ($pixel.A -ne 255) {
                            $destination.SetPixel(
                                $x,
                                $y,
                                [System.Drawing.Color]::FromArgb(255, $pixel.R, $pixel.G, $pixel.B))
                        }
                    }
                }
            }
            finally {
                $graphics.Dispose()
            }

            $destinationDirectory = Split-Path -Parent $DestinationPath
            New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
            $destination.Save($DestinationPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $destination.Dispose()
        }
    }
    finally {
        $source.Dispose()
    }
}

Convert-SpriteSheet `
    -SourcePath (Join-Path $ProjectRoot 'design\generated\character-sprite-source.png') `
    -DestinationPath (Join-Path $ProjectRoot 'src\DeskStealth.App\Assets\characters.png') `
    -Columns 5 `
    -Rows 4

Convert-SpriteSheet `
    -SourcePath (Join-Path $ProjectRoot 'design\generated\office-tile-source.png') `
    -DestinationPath (Join-Path $ProjectRoot 'src\DeskStealth.App\Assets\office-tiles.png') `
    -Columns 5 `
    -Rows 2

Write-Output 'Prepared compact embedded sprite sheets.'
