$es_version = "1.4.4"
if ($env:ES_VERSION) {
	$es_version = $env:ES_VERSION
}

if (!(Test-Path -Path "elasticsearch-$es_version" )) {
    wget "http://download.elasticsearch.org/elasticsearch/elasticsearch/elasticsearch-$es_version.zip" -OutFile "elasticsearch.zip"
}
if (Test-Path -Path "elasticsearch.zip") {
    7z x -y "elasticsearch.zip" > $null
    cp .\elasticsearch.yml .\elasticsearch-$es_version\config -Force
	rm elasticsearch.zip
}

$service = ".\elasticsearch-$es_version\bin\service.bat"
&$service install
&$service start
