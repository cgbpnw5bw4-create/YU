param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
$baselineDirectory = Join-Path $projectRoot 'artifacts\golden'
$applicationDll = Join-Path $projectRoot "src\DeskStealth.App\bin\$Configuration\net10.0-windows\win-x64\DeskStealth.dll"
if (-not (Test-Path -LiteralPath $applicationDll)) {
    throw "Build output not found: $applicationDll"
}

$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) "DeskStealth.Golden.$([Guid]::NewGuid().ToString('N'))"
[System.IO.Directory]::CreateDirectory($temporaryDirectory) | Out-Null
try {
    & dotnet $applicationDll --render-golden $temporaryDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Golden renderer exited with code $LASTEXITCODE."
    }

    foreach ($name in @('game-golden.png', 'clock-golden.png')) {
        $baseline = Join-Path $baselineDirectory $name
        $actual = Join-Path $temporaryDirectory $name
        if (-not (Test-Path -LiteralPath $baseline)) {
            throw "Approved baseline is missing: $baseline"
        }

        $baselineHash = (Get-FileHash -LiteralPath $baseline -Algorithm SHA256).Hash
        $actualHash = (Get-FileHash -LiteralPath $actual -Algorithm SHA256).Hash
        if ($baselineHash -ne $actualHash) {
            throw "$name differs from its approved baseline. Expected $baselineHash; actual $actualHash."
        }

        Write-Output "GOLDEN_OK=$name SHA256=$actualHash"
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        [System.IO.Directory]::Delete($temporaryDirectory, $true)
    }
}
