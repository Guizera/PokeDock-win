param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Publisher,

    [string]$ManifestPath = (Join-Path $PSScriptRoot '..\src\PokeDock.Windows\Package.appxmanifest')
)

$ErrorActionPreference = 'Stop'

$parts = $Version.Split('.') | ForEach-Object { [int]$_ }
if ($parts.Where({ $_ -lt 0 -or $_ -gt 65535 }).Count -gt 0) {
    throw "Every MSIX version component must be between 0 and 65535: $Version"
}

$resolvedManifest = (Resolve-Path $ManifestPath).Path
[xml]$manifest = Get-Content -LiteralPath $resolvedManifest -Raw
$namespace = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
$namespace.AddNamespace('f', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$identity = $manifest.SelectSingleNode('/f:Package/f:Identity', $namespace)
if ($null -eq $identity) {
    throw "Package Identity was not found in $resolvedManifest"
}

$identity.SetAttribute('Version', $Version)
$identity.SetAttribute('Publisher', $Publisher)
$manifest.Save($resolvedManifest)

Write-Host "Configured MSIX identity version $Version for publisher $Publisher."
