<#
.SYNOPSIS
Shows or updates the GolfSG app version in Directory.Build.props.

.EXAMPLE
.\scripts\Set-AppVersion.ps1 -Show

.EXAMPLE
.\scripts\Set-AppVersion.ps1 -Bump Patch

.EXAMPLE
.\scripts\Set-AppVersion.ps1 -Version 1.0.0 -BuildNumber 20
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('Major', 'Minor', 'Patch', 'Build')]
    [string]$Bump,

    [ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$')]
    [string]$Version,

    [ValidateRange(1, 2147483647)]
    [int]$BuildNumber,

    [switch]$Show
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-VersionParts {
    param([Parameter(Mandatory = $true)][string]$Value)

    if ($Value -notmatch '^(?<Major>0|[1-9]\d*)\.(?<Minor>0|[1-9]\d*)\.(?<Patch>0|[1-9]\d*)$') {
        throw "VersionPrefix must be semantic version format: major.minor.patch. Current value: '$Value'."
    }

    [pscustomobject]@{
        Major = [int]$Matches.Major
        Minor = [int]$Matches.Minor
        Patch = [int]$Matches.Patch
    }
}

function Get-RequiredProperty {
    param(
        [Parameter(Mandatory = $true)][System.Xml.XmlElement]$PropertyGroup,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $node = $PropertyGroup.SelectSingleNode($Name)
    if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
        throw "Missing required property '$Name' in Directory.Build.props."
    }

    $node.InnerText.Trim()
}

function Set-ProjectProperty {
    param(
        [Parameter(Mandatory = $true)][System.Xml.XmlDocument]$Document,
        [Parameter(Mandatory = $true)][System.Xml.XmlElement]$PropertyGroup,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $node = $PropertyGroup.SelectSingleNode($Name)
    if ($null -eq $node) {
        $node = $Document.CreateElement($Name)
        [void]$PropertyGroup.AppendChild($node)
    }

    $node.InnerText = $Value
}

function Save-XmlDocument {
    param(
        [Parameter(Mandatory = $true)][System.Xml.XmlDocument]$Document,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Indent = $true
    $settings.OmitXmlDeclaration = $true
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $settings.NewLineChars = "`r`n"

    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    try {
        $Document.Save($writer)
    }
    finally {
        $writer.Dispose()
    }
}

function Write-AppVersion {
    param(
        [Parameter(Mandatory = $true)][string]$DisplayVersion,
        [Parameter(Mandatory = $true)][int]$Build
    )

    Write-Host "GolfSG version: $DisplayVersion"
    Write-Host "Build number:   $Build"
}

if ($Show -and ($PSBoundParameters.ContainsKey('Bump') -or $PSBoundParameters.ContainsKey('Version') -or $PSBoundParameters.ContainsKey('BuildNumber'))) {
    throw 'Use -Show by itself, or use update parameters without -Show.'
}

if ($PSBoundParameters.ContainsKey('Bump') -and $PSBoundParameters.ContainsKey('Version')) {
    throw 'Use either -Bump or -Version, not both.'
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$propsPath = Join-Path $repoRoot 'Directory.Build.props'

[xml]$document = Get-Content -LiteralPath $propsPath -Raw
$propertyGroup = $document.SelectSingleNode('/Project/PropertyGroup[VersionPrefix or ApplicationBuildNumber]')
if ($null -eq $propertyGroup) {
    throw 'Could not find a PropertyGroup with VersionPrefix or ApplicationBuildNumber in Directory.Build.props.'
}

$currentVersion = Get-RequiredProperty -PropertyGroup $propertyGroup -Name 'VersionPrefix'
$currentBuildText = Get-RequiredProperty -PropertyGroup $propertyGroup -Name 'ApplicationBuildNumber'
$currentParts = Get-VersionParts -Value $currentVersion

$currentBuild = 0
if (-not [int]::TryParse($currentBuildText, [ref]$currentBuild) -or $currentBuild -lt 1) {
    throw "ApplicationBuildNumber must be a positive integer. Current value: '$currentBuildText'."
}

$hasUpdate = $PSBoundParameters.ContainsKey('Bump') -or $PSBoundParameters.ContainsKey('Version') -or $PSBoundParameters.ContainsKey('BuildNumber')
if ($Show -or -not $hasUpdate) {
    Write-AppVersion -DisplayVersion $currentVersion -Build $currentBuild
    if (-not $Show) {
        Write-Host ''
        Write-Host 'Use -Bump Major|Minor|Patch|Build or -Version 1.2.3 [-BuildNumber 42] to update it.'
    }
    return
}

if ($PSBoundParameters.ContainsKey('Version')) {
    $nextVersion = $Version
}
elseif ($PSBoundParameters.ContainsKey('Bump')) {
    switch ($Bump) {
        'Major' { $nextVersion = '{0}.0.0' -f ($currentParts.Major + 1) }
        'Minor' { $nextVersion = '{0}.{1}.0' -f $currentParts.Major, ($currentParts.Minor + 1) }
        'Patch' { $nextVersion = '{0}.{1}.{2}' -f $currentParts.Major, $currentParts.Minor, ($currentParts.Patch + 1) }
        'Build' { $nextVersion = $currentVersion }
    }
}
else {
    $nextVersion = $currentVersion
}

if ($PSBoundParameters.ContainsKey('BuildNumber')) {
    $nextBuild = $BuildNumber
}
else {
    $nextBuild = $currentBuild + 1
}

$target = "version $nextVersion, build $nextBuild"
if ($PSCmdlet.ShouldProcess($propsPath, "Set GolfSG $target")) {
    Set-ProjectProperty -Document $document -PropertyGroup $propertyGroup -Name 'VersionPrefix' -Value $nextVersion
    Set-ProjectProperty -Document $document -PropertyGroup $propertyGroup -Name 'ApplicationBuildNumber' -Value ([string]$nextBuild)
    Save-XmlDocument -Document $document -Path $propsPath
    Write-Host "Updated GolfSG from version $currentVersion, build $currentBuild to $target."
}
else {
    Write-Host "Would update GolfSG from version $currentVersion, build $currentBuild to $target."
}