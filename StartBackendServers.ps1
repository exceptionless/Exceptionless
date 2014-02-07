# Copyright 2014 Exceptionless
#
# This program is free software: you can redistribute it and/or modify it 
# under the terms of the GNU Affero General Public License as published 
# by the Free Software Foundation, either version 3 of the License, or 
# (at your option) any later version.
# 
#     http://www.gnu.org/licenses/agpl-3.0.html

Write-Host Starting MongoDB
Start-Process -NoNewWindow -FilePath "$((Get-Location).Path)\libraries\mongo\bin\mongod.exe" -ArgumentList "--journal --dbpath $((Get-Location).Path)\libraries\mongo\data"

Write-Host Starting Redis
Start-Process -NoNewWindow -FilePath "$((Get-Location).Path)\libraries\redis\bin\redis-server.exe" -ArgumentList "$((Get-Location).Path)\libraries\redis\bin\redis.conf" 