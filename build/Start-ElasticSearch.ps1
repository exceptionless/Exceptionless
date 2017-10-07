Param(
    [string]$Version = "5.6.1",
    [int]$NodeCount = 1,
    [switch]$SkipNodeStart = $false,
    [int]$StartPort = 9200,
    [switch]$StartKibana = $false,
    [switch]$OpenKibana = $false,
    [switch]$ResetData = $false
)

If ($env:JAVA_HOME -eq $null -Or -Not(Test-Path -Path $env:JAVA_HOME)) {
    Write-Error "Please ensure the latest version of java is installed and the JAVA_HOME environmental variable has been set."
    $host.SetShouldExit(1)
    Return
}

Push-Location $PSScriptRoot

If ($ResetData) {
    Get-ChildItem "elasticsearch-*-node*\data" | Where-Object { $_.PSIsContainer } | ForEach-Object {
        Write-Output "Resetting node data $_..."
        Remove-Item -Recurse -Force -Path $_
    }
}

If (-Not $SkipNodeStart) {
    If (-Not (Test-Path -Path "elasticsearch-$Version") -And -Not (Test-Path -Path "elasticsearch-$Version.zip")) {
        Write-Output "Downloading Elasticsearch $Version..."
        Invoke-WebRequest "https://artifacts.elastic.co/downloads/elasticsearch/elasticsearch-$Version.zip" -OutFile "elasticsearch-$Version.zip"
    } Else {
        Write-Output "Using already downloaded Elasticsearch $Version..."
    }

    If ((Test-Path -Path "elasticsearch-$Version.zip") -And !(Test-Path -Path "elasticsearch-$Version")) {
        Write-Output "Extracting Elasticsearch $Version..."
        Add-Type -assembly "system.io.compression.filesystem"
        [io.compression.zipfile]::ExtractToDirectory("$PSScriptRoot\elasticsearch-$Version.zip", $PSScriptRoot)
        Remove-Item elasticsearch-$Version.zip

        Write-Output "Installing mapper-size plugin ..."
        Invoke-Expression ".\elasticsearch-$Version\bin\elasticsearch-plugin.bat install mapper-size" | Out-Null
        if ($LastExitCode -ne 0) {
            $host.SetShouldExit($LastExitCode)
            Return
        }
        Write-Output "Done installing mapper-size plugin."

        Write-Output "Installing x-pack plugin..."
        Invoke-Expression ".\elasticsearch-$Version\bin\elasticsearch-plugin.bat install x-pack --batch" | Out-Null
        if ($LastExitCode -ne 0) {
            $host.SetShouldExit($LastExitCode)
            Return
        }
        Write-Output "Done installing x-pack plugin."
    } Else {
        Write-Output "Using already downloaded and extracted Elasticsearch $Version..."
    }

    For ($i = 1; $i -le $NodeCount; $i++) {
        $nodePort = $StartPort + $i - 1
        Write-Output "Starting Elasticsearch $Version node $i port $nodePort"

        If (-Not (Test-Path -Path ".\elasticsearch-$Version-node$i")) {
            Copy-Item .\elasticsearch-$Version .\elasticsearch-$Version-node$i -Recurse
            Copy-Item .\elasticsearch.yml .\elasticsearch-$Version-node$i\config -Force
            Copy-Item .\jvm.options .\elasticsearch-$Version-node$i\config -Force
            Add-Content .\elasticsearch-$Version-node$i\config\elasticsearch.yml "`nhttp.port: $nodePort"
        }

        Start-Process "elasticsearch-$Version-node$i\bin\elasticsearch.bat"

        $attempts = 0
        $success = $false
        Do {
            If ($attempts -gt 0) {
                Start-Sleep -s 2
            }

            Write-Host "Waiting for Elasticsearch $Version node $i to respond ($attempts)..."
            $res = $null

            Try {
                $res = Invoke-WebRequest http://localhost:$nodePort -UseBasicParsing
                If ($res -ne $null -And $res.StatusCode -eq 200) {
                    $success = $true
                    Write-Host "Elasticsearch $Version node $i started."
                    Break
                }
            } Catch {}
            $attempts = $attempts + 1
        } Until ($attempts -gt 15)

        If ($success -eq $false) {
            Write-Error "Failed to start Elasticsearch $Version node $i."
            $host.SetShouldExit($LastExitCode)
            Return
        }
    }
}

If ($StartKibana -Or $OpenKibana) {
    If (-Not (Test-Path -Path "kibana-$Version") -And -Not (Test-Path -Path "kibana-$Version.zip")) {
        Write-Output "Downloading Kibana $Version..."
        Invoke-WebRequest "https://artifacts.elastic.co/downloads/kibana/kibana-$Version-windows-x86.zip" -OutFile "kibana-$Version.zip"
    } Else {
        Write-Output "Using already downloaded Kibana $Version..."
    }

    If ((Test-Path -Path "kibana-$Version.zip") -And -Not (Test-Path -Path "kibana-$Version")) {
        Write-Output "Extracting Kibana $Version..."
        Add-Type -assembly "system.io.compression.filesystem"
        [io.compression.zipfile]::ExtractToDirectory("$PSScriptRoot\kibana-$Version.zip", $PSScriptRoot)
        Rename-Item .\kibana-$Version-windows-x86\ kibana-$Version
        Remove-Item kibana-$Version.zip

        Invoke-Expression ".\kibana-$Version\bin\kibana-plugin.bat install x-pack"
        Add-Content ".\kibana-$Version\config\kibana.yml" "`nxpack.security.enabled: false"
    } Else {
        Write-Output "Using already downloaded and extracted Kibana $Version..."
    }

    Write-Output "Starting Kibana $Version"
    Start-Process "kibana-$Version\bin\kibana.bat"
    $attempts = 0
    $success = $false
    Do {
        If ($attempts -gt 0) {
            Start-Sleep -s 2
        }

        Write-Host "Waiting for Kibana $Version to respond ($attempts)..."
        $res = $null

        Try {
            $res = Invoke-WebRequest http://localhost:5601 -UseBasicParsing
            If ($res -ne $null -And $res.StatusCode -eq 200) {
                $success = $true
                Write-Host "Kibana $Version started."
                Break
            }
        } Catch {}
        $attempts = $attempts + 1
    } Until ($attempts -gt 15)

    If ($success -eq $false) {
        Write-Error "Failed to start Kibana $Version."
        $host.SetShouldExit($LastExitCode)
        Return
    }

    If ($OpenKibana) {
        Write-Host "Opening browser to http://localhost:5601/app/kibana#/dev_tools/console"
        Start-Process "http://localhost:5601/app/kibana#/dev_tools/console"
    }
}

Pop-Location
