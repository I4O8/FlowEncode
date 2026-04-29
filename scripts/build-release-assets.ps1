param(
    [string]$Version,
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputRoot,
    [string[]]$SatelliteLanguageDirectories = @("en-us", "zh-cn")
)

$ErrorActionPreference = "Stop"

function Resolve-MsBuildPath {
    $command = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $vswherePath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswherePath) {
        $installationPath = & $vswherePath -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if (-not [string]::IsNullOrWhiteSpace($installationPath)) {
            $candidate = Join-Path $installationPath "MSBuild\Current\Bin\MSBuild.exe"
            if (Test-Path $candidate) {
                return $candidate
            }
        }
    }

    $candidates = @(
        "E:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    throw "MSBuild.exe was not found. Use Visual Studio MSBuild for this WinUI project."
}

function Get-ProjectVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath
    )

    [xml]$projectXml = Get-Content $ProjectPath -Raw
    $propertyGroups = @($projectXml.Project.PropertyGroup)
    foreach ($group in $propertyGroups) {
        if ($group.Version -and -not [string]::IsNullOrWhiteSpace($group.Version)) {
            return $group.Version.Trim()
        }
    }

    throw "Could not resolve <Version> from $ProjectPath"
}

function Remove-DirectoryIfExists {
    param([string]$Path)

    if (Test-Path $Path) {
        Remove-Item -Path $Path -Recurse -Force
    }
}

function Get-Sha256Hash {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $hashCommand = Get-Command Get-FileHash -ErrorAction SilentlyContinue
    if ($null -ne $hashCommand) {
        return (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToUpperInvariant()
    }

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            $bytes = $sha256.ComputeHash($stream)
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    return (-join ($bytes | ForEach-Object { $_.ToString("x2") })).ToUpperInvariant()
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "FlowEncode\FlowEncode.csproj"
$commonScript = Join-Path $repoRoot "scripts\release-artifact-common.ps1"
$installerBuildScript = Join-Path $repoRoot "scripts\build-installer.ps1"
$msbuildPath = Resolve-MsBuildPath

if (-not (Test-Path $projectPath)) {
    throw "Project was not found: $projectPath"
}

if (-not (Test-Path $commonScript)) {
    throw "Common release helper script was not found: $commonScript"
}

if (-not (Test-Path $installerBuildScript)) {
    throw "Installer build script was not found: $installerBuildScript"
}

. $commonScript

$projectVersion = Get-ProjectVersion -ProjectPath $projectPath
$resolvedVersion = if ([string]::IsNullOrWhiteSpace($Version)) {
    $projectVersion
}
else {
    $Version.Trim()
}

if (-not [string]::Equals($resolvedVersion, $projectVersion, [System.StringComparison]::Ordinal)) {
    throw "Explicit -Version '$resolvedVersion' does not match project version '$projectVersion'. Update FlowEncode.csproj first or omit -Version."
}

$resolvedOutputRoot = if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    Join-Path $repoRoot "artifacts\release\v$resolvedVersion"
}
else {
    if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
        $OutputRoot
    }
    else {
        Join-Path $repoRoot $OutputRoot
    }
}

$publishRoot = Join-Path $repoRoot "artifacts\publish\v$resolvedVersion\app"
$installerBaseName = "FlowEncode_Setup_v$resolvedVersion"
$installerArtifactPath = Join-Path $resolvedOutputRoot "$installerBaseName.exe"
$publishDirArgument = if ($publishRoot.EndsWith("\") -or $publishRoot.EndsWith("/")) { $publishRoot } else { "$publishRoot\" }

Remove-DirectoryIfExists -Path $publishRoot
Remove-DirectoryIfExists -Path $resolvedOutputRoot
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
New-Item -ItemType Directory -Path $resolvedOutputRoot -Force | Out-Null

$buildArgs = @(
    $projectPath,
    "/restore",
    "/t:Publish",
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/p:PublishProfile=win-$Platform",
    "/p:PublishDir=$publishDirArgument",
    "/m:1"
)

Write-Host "Using MSBuild: $msbuildPath"
Write-Host "Publishing version: $resolvedVersion"
& $msbuildPath @buildArgs

if ($LASTEXITCODE -ne 0) {
    throw "MSBuild Publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path (Join-Path $publishRoot "FlowEncode.exe"))) {
    throw "Publish completed but executable was not found in $publishRoot"
}

Sanitize-ReleaseLayout -RootPath $publishRoot -SatelliteLanguageDirectories $SatelliteLanguageDirectories

& $installerBuildScript `
    -SourceDirectoryPath $publishRoot `
    -Version $resolvedVersion `
    -OutputRoot $resolvedOutputRoot `
    -ArtifactName $installerBaseName

if (-not $?) {
    throw "Installer build failed."
}

if (-not (Test-Path $installerArtifactPath)) {
    throw "Installer artifact was not generated: $installerArtifactPath"
}

$installerHash = Get-Sha256Hash -Path $installerArtifactPath

[pscustomobject]@{
    Version = $resolvedVersion
    InstallerExe = $installerArtifactPath
    InstallerExeSha256 = $installerHash
} | Format-List
