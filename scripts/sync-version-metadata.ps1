param(
    [switch]$Check
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "repo-build-common.ps1")

function Update-ReadmeBadge {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Version
    )

    $content = Get-Content -Path $Path -Raw
    $updated = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        'https://img\.shields\.io/badge/version-[^-"]+-167a7f',
        "https://img.shields.io/badge/version-$Version-167a7f")

    if ([string]::Equals($content, $updated, [System.StringComparison]::Ordinal)) {
        return $false
    }

    if ($Check) {
        throw "Version badge is out of sync: $Path"
    }

    Set-Content -Path $Path -Value $updated -NoNewline
    return $true
}

function Update-ManifestVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$VersionInfo
    )

    $content = Get-Content -Path $Path -Raw
    $updated = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        '(?<=<Identity\b[^>]*\bVersion=")[^"]+(?=")',
        $VersionInfo,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

    if ([string]::Equals($content, $updated, [System.StringComparison]::Ordinal)) {
        return $false
    }

    if ($Check) {
        throw "Version metadata is out of sync: $Path"
    }

    Set-Content -Path $Path -Value $updated -NoNewline
    return $true
}

$repoRoot = Get-RepositoryRoot
$versionInfo = Get-FlowEncodeVersionInfo
$manifestPath = Join-Path $repoRoot "FlowEncode\Package.appxmanifest"
$readmePaths = @(
    (Join-Path $repoRoot "README.md"),
    (Join-Path $repoRoot "README.en.md")
)

$changedFiles = New-Object System.Collections.Generic.List[string]

if (Update-ManifestVersion -Path $manifestPath -VersionInfo $versionInfo.VersionInfo) {
    $changedFiles.Add($manifestPath)
}

foreach ($readmePath in $readmePaths) {
    if (Update-ReadmeBadge -Path $readmePath -Version $versionInfo.Version) {
        $changedFiles.Add($readmePath)
    }
}

if ($Check -and $changedFiles.Count -eq 0) {
    Write-Host "Version metadata is synchronized."
}
elseif (-not $Check -and $changedFiles.Count -eq 0) {
    Write-Host "No version metadata updates were required."
}
elseif (-not $Check) {
    Write-Host "Updated version metadata:"
    foreach ($file in $changedFiles) {
        Write-Host " - $file"
    }
}
