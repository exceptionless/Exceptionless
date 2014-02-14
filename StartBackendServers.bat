:: Copyright 2014 Exceptionless
::
:: This program is free software: you can redistribute it and/or modify it 
:: under the terms of the GNU Affero General Public License as published 
:: by the Free Software Foundation, either version 3 of the License, or 
:: (at your option) any later version.
:: 
::     http://www.gnu.org/licenses/agpl-3.0.html

:: Starting MongoDB
START .\libraries\mongo\bin\mongod.exe --journal --dbpath .\libraries\mongo\data

:: Starting Redis
START .\libraries\redis\bin\redis-server.exe .\libraries\redis\bin\redis.conf