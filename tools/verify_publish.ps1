param(
    [string]$PublishDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\publish')
)

$ErrorActionPreference = 'Stop'
$resolvedDirectory = [System.IO.Path]::GetFullPath($PublishDirectory)
if (-not (Test-Path -LiteralPath $resolvedDirectory -PathType Container)) {
    throw "Publish directory does not exist: $resolvedDirectory"
}

$entries = @(Get-ChildItem -LiteralPath $resolvedDirectory -Force)
if ($entries.Count -ne 1 -or
    $entries[0].PSIsContainer -or
    $entries[0].Name -ne 'DeskStealth.exe') {
    $names = ($entries | ForEach-Object Name) -join ', '
    throw "Publish directory must contain only DeskStealth.exe; found: $names"
}

$executable = $entries[0]
if ($executable.Length -le 0) {
    throw "Published executable is empty: $($executable.FullName)"
}

$hash = (Get-FileHash -LiteralPath $executable.FullName -Algorithm SHA256).Hash
Write-Output "PUBLISH_OK=$($executable.FullName) BYTES=$($executable.Length) SHA256=$hash"
