Param(
  [string]$Version = "5.1.1",
  [int]$NodeCount = 1,
  [bool]$StartKibana = $true,
  [int]$StartPort = 9200,
  [bool]$OpenKibana = $true
)

If ($env:JAVA_HOME -eq $null -or !(Test-Path -Path $env:JAVA_HOME)) {
    Write-Error "Please ensure the latest version of java is installed and the JAVA_HOME environmental variable has been set."
    Return
}

Push-Location $PSScriptRoot

If (!(Test-Path -Path "elasticsearch-$Version") -And !(Test-Path -Path "elasticsearch-$Version.zip")) {
    Write-Output "Downloading Elasticsearch $Version..."
    Invoke-WebRequest "https://artifacts.elastic.co/downloads/elasticsearch/elasticsearch-$Version.zip" -OutFile "elasticsearch-$Version.zip"
} Else {
    Write-Output "Using already downloaded Kibana $Version..."
}

If ((Test-Path -Path "elasticsearch-$Version.zip") -And !(Test-Path -Path "elasticsearch-$Version")) {
    Write-Output "Extracting Elasticsearch $Version..."
    Add-Type -assembly "system.io.compression.filesystem"
    [io.compression.zipfile]::ExtractToDirectory("$PSScriptRoot\elasticsearch-$Version.zip", $PSScriptRoot)
    rm elasticsearch-$Version.zip

    & ".\elasticsearch-$Version\bin\elasticsearch-plugin.bat" install mapper-size 
} Else {
    Write-Output "Using already downloaded and extracted Elasticsearch $Version..."
}

For ($i = 1; $i -le $NodeCount; $i++) {
    $nodePort = $StartPort + $i - 1
	Write-Output "Starting Elasticsearch $Version node $i port $nodePort"
	If (!(Test-Path -Path ".\elasticsearch-$Version-node$i")) {
		cp .\elasticsearch-$Version .\elasticsearch-$Version-node$i -Recurse
        cp .\elasticsearch.yml .\elasticsearch-$Version-node$i\config -Force
        Add-Content .\elasticsearch-$Version-node$i\config\elasticsearch.yml "`nhttp.port: $nodePort"
	}

	Start-Process "$(Get-Location)\elasticsearch-$Version-node$i\bin\elasticsearch.bat"

    $retries = 0
    Do {
        Write-Host "Waiting for Elasticsearch $Version node $i to respond..."
        $res = $null
        
        Try {
            $res = Invoke-WebRequest http://localhost:$nodePort -UseBasicParsing
        } Catch {
            $retries = $retries + 1
            Start-Sleep -s 1
        }
    } Until ($res -ne $null -and $res.StatusCode -eq 200 -and $retries -lt 10)
}

If ($StartKibana -eq $true) {
    If (!(Test-Path -Path "kibana-$Version") -And !(Test-Path -Path "kibana-$Version.zip")) {
	    Write-Output "Downloading Kibana $Version..."
        Invoke-WebRequest "https://artifacts.elastic.co/downloads/kibana/kibana-$Version-windows-x86.zip" -OutFile "kibana-$Version.zip"
    } Else {
	    Write-Output "Using already downloaded Kibana $Version..."
    }

    If ((Test-Path -Path "kibana-$Version.zip") -And !(Test-Path -Path "kibana-$Version")) {
	    Write-Output "Extracting Kibana $Version..."
        Add-Type -assembly "system.io.compression.filesystem"
        [io.compression.zipfile]::ExtractToDirectory("$PSScriptRoot\kibana-$Version.zip", $PSScriptRoot)
        Rename-Item .\kibana-$Version-windows-x86\ kibana-$Version
        rm kibana-$Version.zip
    } Else {
	    Write-Output "Using already downloaded and extracted Kibana $Version..."
    }

	Write-Output "Starting Kibana $Version"
    Start-Process "$(Get-Location)\kibana-$Version\bin\kibana.bat"
    $retries = 0
    Do {
        Write-Host "Waiting for Kibana $Version to respond..."
        $res = $null
        
        Try {
            $res = Invoke-WebRequest http://localhost:5601 -UseBasicParsing
        } Catch {
            $retries = $retries + 1
            Start-Sleep -s 1
        }
    } Until ($res -ne $null -and $res.StatusCode -eq 200 -and $retries -lt 10)

    If ($OpenKibana) {
        Start-Process "http://localhost:5601/app/kibana#/dev_tools/console"
    }
}

Pop-Location