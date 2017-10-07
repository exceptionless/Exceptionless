Param(
  [string]$Version,
  [string]$Suffix
)

Push-Location $PSScriptRoot

[xml]$versionXml = Get-Content .\version.props
$versionXml.Project.PropertyGroup.VersionPrefix = $Version
$versionXml.Project.PropertyGroup.VersionSuffix = $Suffix
$versionXml.Save("$PSScriptRoot\version.props")

Pop-Location