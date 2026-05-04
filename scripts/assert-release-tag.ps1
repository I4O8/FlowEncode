param(
    [Parameter(Mandatory = $true)]
    [string]$TagRef,
    [switch]$SkipHeadTagCheck
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "repo-build-common.ps1")

function Resolve-TagName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Ref
    )

    if ($Ref.StartsWith("refs/tags/", [System.StringComparison]::Ordinal)) {
        return $Ref.Substring("refs/tags/".Length)
    }

    return $Ref.Trim()
}

$versionInfo = Get-FlowEncodeVersionInfo
$tagName = Resolve-TagName -Ref $TagRef
$expectedTagName = "v$($versionInfo.Version)"

if ([string]::IsNullOrWhiteSpace($tagName)) {
    throw "Release tag could not be resolved from '$TagRef'."
}

if (-not [string]::Equals($tagName, $expectedTagName, [System.StringComparison]::Ordinal)) {
    throw "Release tag '$tagName' does not match project version '$($versionInfo.Version)'. Expected '$expectedTagName'."
}

if (-not $SkipHeadTagCheck) {
    $headTags = @(
        git tag --points-at HEAD
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to enumerate tags that point at HEAD."
    }

    if (-not ($headTags -contains $tagName)) {
        throw "Current commit is not tagged as '$tagName'. Checkout the tagged commit before publishing a release."
    }
}

Write-Host "Release tag matches project version: $tagName"
