param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Publisher,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [uri]$AppInstallerUri,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [uri]$BundleUri,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

if ([version]$Version -lt [version]'1.0.0.0') {
    throw 'App Installer package versions must use a non-zero major component for Windows 10 compatibility.'
}

$settings = New-Object System.Xml.XmlWriterSettings
$settings.Indent = $true
$settings.Encoding = New-Object System.Text.UTF8Encoding($false)
$settings.OmitXmlDeclaration = $false

$outputDirectory = Split-Path -Parent $OutputPath
if ($outputDirectory) {
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
}

$writer = [System.Xml.XmlWriter]::Create($OutputPath, $settings)
try {
    # The 2017/2 schema is supported from Windows 10 version 1803 onward.
    # Keep the feed on this schema so Windows 10 can parse it; newer 2021-only
    # attributes are not required for the update behavior used by PokeDock.
    $namespace = 'http://schemas.microsoft.com/appx/appinstaller/2017/2'
    $writer.WriteStartDocument()
    $writer.WriteStartElement('AppInstaller', $namespace)
    $writer.WriteAttributeString('Version', $Version)
    $writer.WriteAttributeString('Uri', $AppInstallerUri.AbsoluteUri)

    $writer.WriteStartElement('MainBundle', $namespace)
    $writer.WriteAttributeString('Name', 'PokeDock.Windows')
    $writer.WriteAttributeString('Publisher', $Publisher)
    $writer.WriteAttributeString('Version', $Version)
    $writer.WriteAttributeString('Uri', $BundleUri.AbsoluteUri)
    $writer.WriteEndElement()

    $writer.WriteStartElement('UpdateSettings', $namespace)
    $writer.WriteStartElement('OnLaunch', $namespace)
    $writer.WriteAttributeString('HoursBetweenUpdateChecks', '4')
    $writer.WriteEndElement()
    $writer.WriteElementString('AutomaticBackgroundTask', $namespace, '')
    $writer.WriteEndElement()

    $writer.WriteEndElement()
    $writer.WriteEndDocument()
}
finally {
    $writer.Dispose()
}

[xml]$validation = Get-Content -LiteralPath $OutputPath -Raw
if ($validation.DocumentElement.NamespaceURI -ne $namespace) {
    throw "Generated App Installer uses an unexpected schema: $($validation.DocumentElement.NamespaceURI)"
}
if ($validation.AppInstaller.MainBundle.Version -ne $Version) {
    throw "Generated App Installer has an unexpected bundle version."
}

Write-Host "Created $OutputPath for MSIX bundle $Version."
