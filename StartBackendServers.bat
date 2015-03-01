CD Libraries\Mongo
SET MongoDir=%cd%
.\bin\mongod.exe --install -f %cd%\mongod.cfg
net start mongodb
CD ..
PowerShell .\Start-Elasticsearch.ps1
