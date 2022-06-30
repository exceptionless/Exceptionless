ARG UI_VERSION="ui:3.1.10"
FROM exceptionless/${UI_VERSION} AS ui

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /app

COPY ./*.sln ./NuGet.Config ./
COPY ./build/*.props ./build/
COPY ./packages/* ./packages/

# Copy the main source project files
COPY src/*/*.csproj ./
RUN for file in $(ls *.csproj); do mkdir -p src/${file%.*}/ && mv $file src/${file%.*}/; done

# Copy the test project files
COPY tests/*/*.csproj ./
RUN for file in $(ls *.csproj); do mkdir -p tests/${file%.*}/ && mv $file tests/${file%.*}/; done

RUN dotnet restore

# Copy everything else and build app
COPY . .
RUN dotnet build -c Release

# testrunner

FROM build AS testrunner
WORKDIR /app/tests/Exceptionless.Tests
ENTRYPOINT dotnet test --results-directory /app/artifacts --logger:trx

# job-publish

FROM build AS job-publish
WORKDIR /app/src/Exceptionless.Job

RUN dotnet publish -c Release -o out

# job

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS job
WORKDIR /app
COPY --from=job-publish /app/src/Exceptionless.Job/out ./
ENTRYPOINT [ "dotnet", "Exceptionless.Job.dll" ]

# api-publish

FROM build AS api-publish
WORKDIR /app/src/Exceptionless.Web

RUN dotnet publish -c Release -o out

# api

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS api
WORKDIR /app
COPY --from=api-publish /app/src/Exceptionless.Web/out ./
ENTRYPOINT [ "dotnet", "Exceptionless.Web.dll" ]

# app

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS app

WORKDIR /app
COPY --from=api-publish /app/src/Exceptionless.Web/out ./
COPY --from=ui /app ./wwwroot
COPY --from=ui /usr/local/bin/update-config /usr/local/bin/update-config
COPY ./build/app-docker-entrypoint.sh ./

ENV EX_ConnectionStrings__Storage=provider=folder;path=/app/storage \
    EX_RunJobsInProcess=true \
    ASPNETCORE_URLS=http://+:80 \
    EX_Html5Mode=true

RUN chmod +x /app/app-docker-entrypoint.sh

EXPOSE 80

ENTRYPOINT ["/app/app-docker-entrypoint.sh"]

# completely self-contained

FROM exceptionless/elasticsearch:8.3.1 AS exceptionless

WORKDIR /app
COPY --from=job-publish /app/src/Exceptionless.Job/out ./
COPY --from=api-publish /app/src/Exceptionless.Web/out ./
COPY --from=ui /app ./wwwroot
COPY --from=ui /usr/local/bin/update-config /usr/local/bin/update-config
COPY ./build/docker-entrypoint.sh ./
COPY ./build/supervisord.conf /etc/

USER root

# install dotnet and supervisor
RUN apt-get update -y && \
    apt-get install -y --no-install-recommends \
        wget \
        apt-transport-https \
        supervisor \
        dos2unix \
        libc6 \
        libgcc1 \
        libgssapi-krb5-2 \
        libicu66 \
        libssl1.1 \
        libstdc++6 \
        zlib1g && \
    dos2unix /app/docker-entrypoint.sh

ENV discovery.type=single-node \
    xpack.security.enabled=false \
    ES_JAVA_OPTS="-Xms1g -Xmx1g" \
    ASPNETCORE_URLS=http://+:80 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    EX_ConnectionStrings__Storage=provider=folder;path=/app/storage \
    EX_ConnectionStrings__Elasticsearch=server=http://localhost:9200 \
    EX_RunJobsInProcess=true \
    EX_Html5Mode=true

RUN chmod +x /app/docker-entrypoint.sh && \
    chown -R elasticsearch:elasticsearch /app && \
    mkdir -p /var/log/supervisor >/dev/null 2>&1 && \
    chown -R elasticsearch:elasticsearch /var/log/supervisor

USER elasticsearch

RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x dotnet-install.sh  && \
    ./dotnet-install.sh -c 6.0 --runtime aspnetcore && \
    rm dotnet-install.sh

EXPOSE 80 9200

ENTRYPOINT ["/app/docker-entrypoint.sh"]

# build locally
# docker buildx build --target exceptionless --platform linux/amd64,linux/arm64 --load --tag exceptionless .