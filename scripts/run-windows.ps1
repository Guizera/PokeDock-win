param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("arm64", "x64")]
    [string]$Architecture = "arm64"
)

$ErrorActionPreference = "Stop"

$sourceRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $env:LOCALAPPDATA "PokeDock\Workspace"
$projectPath = Join-Path $localRoot "src\PokeDock.Windows\PokeDock.Windows.csproj"

Write-Host "Synchronizing PokeDock to $localRoot..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $localRoot -Force | Out-Null

& robocopy $sourceRoot $localRoot /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS /NP `
    /XD .git .vs bin obj TestResults `
    /XF *.user *.suo

$copyExitCode = $LASTEXITCODE
if ($copyExitCode -ge 8) {
    throw "Could not synchronize the project. Robocopy exit code: $copyExitCode"
}

Write-Host "Launching PokeDock ($Configuration | $Architecture)..." -ForegroundColor Green
Push-Location $localRoot
try {
    & winapp run $projectPath -c $Configuration --arch $Architecture
    if ($LASTEXITCODE -ne 0) {
        throw "winapp failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
