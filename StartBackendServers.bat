CD Libraries\Mongo
.\bin\mongod.exe --install -f .\mongod.cfg
net start mongodb
CD ..
PowerShell .\Start-Elasticsearch.ps1
