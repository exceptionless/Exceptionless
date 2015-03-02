$es_version = "1.4.4"
if ($env:ES_VERSION) {
    $es_version = $env:ES_VERSION
}

Push-Location $PSScriptRoot

if (!(Test-Path -Path "elasticsearch-$es_version") -And !(Test-Path -Path "elasticsearch.zip")) {
    wget "http://download.elasticsearch.org/elasticsearch/elasticsearch/elasticsearch-$es_version.zip" -OutFile "elasticsearch.zip"
}

if (Test-Path -Path "elasticsearch.zip") {
    7z x -y "elasticsearch.zip" > $null
    cp .\elasticsearch.yml .\elasticsearch-$es_version\config -Force
    rm elasticsearch.zip
}

Start-Process -NoNewWindow "$(Get-Location)\elasticsearch-$es_version\bin\elasticsearch.bat"

Pop-Location
