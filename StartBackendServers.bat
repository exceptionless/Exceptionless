START .\libraries\mongo\bin\mongod.exe --journal --dbpath .\libraries\mongo\data

CD .\libraries\redis\bin
START .\redis-server.exe .\redis.conf
CD ..\..\..\