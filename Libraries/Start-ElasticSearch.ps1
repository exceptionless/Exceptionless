$env:ElasticSearch_Version = "1.4.4"

if(!(Test-Path -Path "elasticsearch-$env:ElasticSearch_Version" )){
    wget "http://download.elasticsearch.org/elasticsearch/elasticsearch/elasticsearch-$env:ElasticSearch_Version.zip" -OutFile "elasticsearch-$env:ElasticSearch_Version.zip"
    7z x "elasticsearch-$env:ElasticSearch_Version.zip"
    rm "elasticsearch-$env:ElasticSearch_Version.zip"
}

elasticsearch-1.4.4\bin\service.bat install
elasticsearch-1.4.4\bin\service.bat start