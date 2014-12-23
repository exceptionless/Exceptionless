function Update-ApplicationConfig
{
    param (
        $configPath,
        $variables
    )

    [xml]$xml = New-Object XML
    $xml.Load($configPath)

    # appSettings section
    if ($variables.ContainsKey("appSettings")) {
        $appSettingsNode = $xml.SelectSingleNode("configuration/appSettings")
        if ($appSettingsNode -eq $null) {
          $appSettingsNode = $xml.CreateElement('appSettings')
          $xml.SelectSingleNode("configuration").AppendChild($appSettingsNode)
        }
        foreach ($appSettingKey in $variables["appSettings"].keys) {
          $settingNode = $appSettingsNode.SelectSingleNode("add[@key='$appSettingKey']")
          if ($settingNode -eq $null) {
            $settingNode = $xml.CreateElement("add")
            $settingNode.SetAttribute('key', $appSettingKey)
            $appSettingsNode.AppendChild($settingNode)
          }
          $value = $variables["appSettings"][$appSettingKey]
          $settingNode.SetAttribute('value', $value)
          Write-Host "Updating <appSettings> entry `"$appSettingKey`" to `"$value`""
        }
    }
    
    # connectionStrings
    if ($variables.ContainsKey("connectionStrings")) {
        $connectionStringsNode = $xml.SelectSingleNode("configuration/connectionStrings")
        if ($connectionStringsNode -eq $null) {
          $connectionStringsNode = $xml.CreateElement('connectionStrings')
          $xml.SelectSingleNode("configuration").AppendChild($connectionStringsNode)
        }

        foreach ($connectionStringName in $variables["connectionStrings"].keys) {
          $connectionStringNode = $connectionStringsNode.SelectSingleNode("add[@name='$connectionStringName']")
          if ($connectionStringNode -eq $null) {
            $connectionStringNode = $xml.CreateElement("add")
            $connectionStringNode.SetAttribute('name', $connectionStringName)
            $connectionStringsNode.AppendChild($connectionStringNode)
          }
          $value = $variables["connectionStrings"][$connectionStringName]
          $connectionStringNode.SetAttribute('connectionString', $value)
          Write-Host "Updating <connectionStrings> entry `"$connectionStringName`" to `"$value`""
        }
    }
    
    # xmlpoke
    if ($variables.ContainsKey("xmlpoke")) {
        foreach ($xpath in $variables["xmlpoke"].keys) {
            $value = $variables["xmlpoke"][$xpath]
            $node = $xml.SelectSingleNode($xpath)
            if ($node) { 
                $node.Value = $value 
                Write-Host "XmlPoke setting `"$xpath`" to `"$value`""
            } else {
                $index = $xpath.LastIndexOf('/');
                $nodePath = $xpath.Substring(0, $index);
                $attrName = $xpath.Substring($index + 2);
                $node = $xml.SelectSingleNode($nodePath)
                if ($node) {
                    $node.SetAttribute($attrName, $value)
                } else {
                    Write-Host "XmlPoke setting `"$xpath`" not found"
                }
            }
        }
    }

    $xml.Save($configPath)
}

$config = @{}

foreach ($param in $OctopusParameters.keys) {
  if ($param.StartsWith("appSettings.")) {
    if ($config["appSettings"] -eq $null) {
        $config["appSettings"] = @{}
    }
    $config["appSettings"][$param.Substring(12)] = $OctopusParameters[$param]
  }
  if ($param.StartsWith("connectionStrings.")) {
    if ($config["connectionStrings"] -eq $null) {
        $config["connectionStrings"] = @{}
    }
    $config["connectionStrings"][$param.Substring(18)] = $OctopusParameters[$param]
  }
  if ($param.StartsWith("xmlpoke.")) {
    if ($config["xmlpoke"] -eq $null) {
        $config["xmlpoke"] = @{}
    }
    $config["xmlpoke"][$param.Substring(8)] = $OctopusParameters[$param]
  }
}

if (Test-Path ($OctopusParameters["Octopus.Action.Package.CustomInstallationDirectory"] + "\Web.config")) {
  $configPath = $OctopusParameters["Octopus.Action.Package.CustomInstallationDirectory"] + "\Web.config"
} else {
  $configPath = $OctopusParameters["OctopusOriginalPackageDirectoryPath"] + "\Web.config"
}

if (Test-Path $configPath) {
  Update-ApplicationConfig -configPath $configPath -variables $config
} else {
  Write-Host "Could not resolve config path."
}

if (Test-Path ($OctopusParameters["Octopus.Action.Package.CustomInstallationDirectory"] + "\App_Data\JobRunner\Job.exe.config")) {
  $jobConfigPath = $OctopusParameters["Octopus.Action.Package.CustomInstallationDirectory"] + "\App_Data\JobRunner\Job.exe.config"
} else {
  $jobConfigPath = $OctopusParameters["OctopusOriginalPackageDirectoryPath"] + "\App_Data\JobRunner\Job.exe.config"
}

if (Test-Path $jobConfigPath) {
  Update-ApplicationConfig -configPath $jobConfigPath -variables $config
} else {
  Write-Host "Could not resolve job config path."
}

gci App_Data\jobs -recurse -filter "run.bat" -File -ErrorAction SilentlyContinue | % { Add-Content $_.FullName ("`r`n`r`nREM Revision Date: {0}" -f (Get-Date)) }
