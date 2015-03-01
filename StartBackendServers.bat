START .\Libraries\Mongo\bin\mongod.exe --journal --dbpath .\Libraries\Mongo\data
CD Libraries
START PowerShell .\Start-Elasticsearch.ps1
