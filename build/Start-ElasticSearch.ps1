$es_version = "5.1.1"
If ($env:ES_VERSION) {
    $es_version = $env:ES_VERSION
}

If ($env:JAVA_HOME -eq $null -or !(Test-Path -Path $env:JAVA_HOME)) {
    Write-Error "Please ensure the latest version of java is installed and the JAVA_HOME environmental variable has been set."
    Return
}

Push-Location $PSScriptRoot

If (!(Test-Path -Path "elasticsearch-$es_version") -And !(Test-Path -Path "elasticsearch.zip")) {
    Invoke-WebRequest "https://artifacts.elastic.co/downloads/elasticsearch/elasticsearch-$es_version.zip" -OutFile "elasticsearch-$es_version.zip"
}

If ((Test-Path -Path "elasticsearch-$es_version.zip") -And !(Test-Path -Path "elasticsearch-$es_version")) {
    Add-Type -assembly "system.io.compression.filesystem"
    [io.compression.zipfile]::ExtractToDirectory("$PSScriptRoot\elasticsearch-$es_version.zip", $PSScriptRoot)
    cp .\elasticsearch.yml .\elasticsearch-$es_version\config -Force
    rm elasticsearch-$es_version.zip
    & ".\elasticsearch-$es_version\bin\elasticsearch-plugin.bat" install mapper-size
}

Start-Process -NoNewWindow "$(Get-Location)\elasticsearch-$es_version\bin\elasticsearch.bat"

Pop-Location