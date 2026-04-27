param(
    [string]$SourceImagePath,
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($SourceImagePath)) {
    $SourceImagePath = Join-Path $repoRoot "FlowEncode\Assets\FlowEncodeHammer.png"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "installer\assets"
}

if (-not (Test-Path $SourceImagePath)) {
    throw "Source installer art was not found: $SourceImagePath"
}

Add-Type -AssemblyName System.Drawing

function Export-CroppedImage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputPath,
        [Parameter(Mandatory = $true)]
        [string]$OutputPath,
        [Parameter(Mandatory = $true)]
        [int]$Width,
        [Parameter(Mandatory = $true)]
        [int]$Height
    )

    $sourceImage = [System.Drawing.Image]::FromFile($InputPath)

    try {
        $sourceWidth = [double]$sourceImage.Width
        $sourceHeight = [double]$sourceImage.Height
        $targetRatio = [double]$Width / [double]$Height
        $sourceRatio = $sourceWidth / $sourceHeight

        if ($sourceRatio -gt $targetRatio) {
            $cropHeight = $sourceHeight
            $cropWidth = $sourceHeight * $targetRatio
            $cropX = ($sourceWidth - $cropWidth) / 2.0
            $cropY = 0.0
        }
        else {
            $cropWidth = $sourceWidth
            $cropHeight = $sourceWidth / $targetRatio
            $cropX = 0.0
            $cropY = ($sourceHeight - $cropHeight) / 2.0
        }

        $bitmap = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

            $destinationRectangle = [System.Drawing.Rectangle]::new(0, 0, $Width, $Height)
            $graphics.DrawImage(
                $sourceImage,
                $destinationRectangle,
                [single]$cropX,
                [single]$cropY,
                [single]$cropWidth,
                [single]$cropHeight,
                [System.Drawing.GraphicsUnit]::Pixel)
            $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
    finally {
        $sourceImage.Dispose()
    }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

Export-CroppedImage -InputPath $SourceImagePath -OutputPath (Join-Path $OutputDirectory "WizardImage.png") -Width 430 -Height 824
Export-CroppedImage -InputPath $SourceImagePath -OutputPath (Join-Path $OutputDirectory "WizardSmallImage.png") -Width 124 -Height 124

Write-Host "Installer art exported to $OutputDirectory"
