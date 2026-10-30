param(
    [Parameter(Mandatory = $true)]
    [string]$BundlePath
)

$ErrorActionPreference = 'Stop'
$resolvedBundle = (Resolve-Path -LiteralPath $BundlePath).Path
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("pokedock-package-" + [guid]::NewGuid().ToString('N'))

try {
    [IO.Compression.ZipFile]::ExtractToDirectory($resolvedBundle, $temporaryDirectory)
    $architectures = @{}

    foreach ($package in Get-ChildItem -LiteralPath $temporaryDirectory -Filter *.msix -File) {
        $packageDirectory = Join-Path $temporaryDirectory $package.BaseName
        [IO.Compression.ZipFile]::ExtractToDirectory($package.FullName, $packageDirectory)

        $manifestPath = Join-Path $packageDirectory 'AppxManifest.xml'
        if (-not (Test-Path -LiteralPath $manifestPath)) {
            throw "Package $($package.Name) does not contain AppxManifest.xml."
        }

        [xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
        $architecture = $manifest.Package.Identity.ProcessorArchitecture
        if ($architecture -notin @('x64', 'arm64')) {
            continue
        }

        $watchdogPath = Join-Path $packageDirectory 'PokeDock.TaskbarWatchdog.exe'
        if (-not (Test-Path -LiteralPath $watchdogPath)) {
            throw "Package $($package.Name) does not contain PokeDock.TaskbarWatchdog.exe."
        }

        $architectures[$architecture] = $true
        Write-Host "Verified native taskbar watchdog in $architecture package."
    }

    foreach ($requiredArchitecture in @('x64', 'arm64')) {
        if (-not $architectures.ContainsKey($requiredArchitecture)) {
            throw "The bundle does not contain a verified $requiredArchitecture package."
        }
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
