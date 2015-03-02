CD Libraries\Mongo
SET MongoDir=%cd%
.\bin\mongod.exe --config %cd%\mongod.cfg --install
NET START mongodb
CD ..
PowerShell .\Start-Elasticsearch.ps1
