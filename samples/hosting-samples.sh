#!/bin/bash

# simple instance for testing
docker run --rm -it -p 5200:8080 exceptionless/exceptionless:latest

# persist data
docker run --rm -it -p 5200:8080 \
    -v ~/esdata:/usr/share/elasticsearch/data \
    exceptionless/exceptionless:latest

# persist data, use ssl, enable mail sending
docker run --rm -it -p 5200:8080 -p 5201:443 \
    -e EX_ConnectionStrings__Email=smtps://user:password@smtp.host.com:587 \
    -e ASPNETCORE_URLS="https://+;http://+" \
    -e ASPNETCORE_HTTPS_PORT=5201 \
    -e ASPNETCORE_Kestrel__Certificates__Default__Password="password" \
    -e ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx \
    -v ~/.aspnet/https:/https/ \
    -v ~/esdata:/usr/share/elasticsearch/data \
    exceptionless/exceptionless:latest

# run the datamigration job
docker run --rm -it exceptionless/exceptionless:latest DataMigration
