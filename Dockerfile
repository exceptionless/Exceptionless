FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY ./*.sln ./NuGet.Config ./
COPY ./src/*.props ./src/
COPY ./tests/*.props ./tests/
COPY ./build/packages/* ./build/packages/
COPY ./docker/docker-compose.dcproj ./docker/

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

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS job
WORKDIR /app
COPY --from=job-publish /app/src/Exceptionless.Job/out ./

EXPOSE 8080

ENTRYPOINT [ "dotnet", "Exceptionless.Job.dll" ]

# api-publish

FROM build AS api-publish
WORKDIR /app/src/Exceptionless.Web

RUN apt-get update -yq
RUN curl -sL https://deb.nodesource.com/setup_18.x | bash - && apt-get install -yq nodejs

RUN dotnet publish -c Release -o out /p:SkipSpaPublish=true

# api

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS api
WORKDIR /app
COPY --from=api-publish /app/src/Exceptionless.Web/out ./

EXPOSE 8080

ENTRYPOINT [ "dotnet", "Exceptionless.Web.dll" ]

# app-publish

FROM build AS app-publish
WORKDIR /app/src/Exceptionless.Web

RUN apt-get update -yq
RUN curl -sL https://deb.nodesource.com/setup_18.x | bash - && apt-get install -yq nodejs

RUN dotnet publish -c Release -o out

# app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS app

WORKDIR /app
COPY --from=app-publish /app/src/Exceptionless.Web/out ./
COPY ./build/app-docker-entrypoint.sh ./
COPY ./build/update-config.sh /usr/local/bin/update-config

ENV EX_ConnectionStrings__Storage=provider=folder;path=/app/storage \
    EX_RunJobsInProcess=true \
    ASPNETCORE_URLS=http://+:8080 \
    EX_Html5Mode=true

RUN chmod +x /app/app-docker-entrypoint.sh
RUN chmod +x /usr/local/bin/update-config

EXPOSE 8080

ENTRYPOINT ["/app/app-docker-entrypoint.sh"]

# completely self-contained

FROM exceptionless/elasticsearch:8.11.0 AS exceptionless

WORKDIR /app
COPY --from=job-publish /app/src/Exceptionless.Job/out ./
COPY --from=app-publish /app/src/Exceptionless.Web/out ./
COPY ./build/docker-entrypoint.sh ./
COPY ./build/update-config.sh /usr/local/bin/update-config
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
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    EX_ConnectionStrings__Storage=provider=folder;path=/app/storage \
    EX_ConnectionStrings__Elasticsearch=server=http://localhost:9200 \
    EX_RunJobsInProcess=true \
    EX_Html5Mode=true

RUN chmod +x /app/docker-entrypoint.sh && \
    chmod +x /usr/local/bin/update-config && \
    chown -R elasticsearch:elasticsearch /app && \
    mkdir -p /var/log/supervisor >/dev/null 2>&1 && \
    chown -R elasticsearch:elasticsearch /var/log/supervisor

USER elasticsearch

RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --channel 8.0 --quality ga --runtime aspnetcore && \
    rm dotnet-install.sh

EXPOSE 8080 9200

ENTRYPOINT ["/app/docker-entrypoint.sh"]

# completely self-contained 7.x

FROM exceptionless/elasticsearch:7.17.14 AS exceptionless7

WORKDIR /app
COPY --from=job-publish /app/src/Exceptionless.Job/out ./
COPY --from=app-publish /app/src/Exceptionless.Web/out ./
COPY ./build/docker-entrypoint.sh ./
COPY ./build/update-config.sh /usr/local/bin/update-config
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
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    EX_ConnectionStrings__Storage=provider=folder;path=/app/storage \
    EX_ConnectionStrings__Elasticsearch=server=http://localhost:9200 \
    EX_RunJobsInProcess=true \
    EX_Html5Mode=true

RUN chmod +x /app/docker-entrypoint.sh && \
    chmod +x /usr/local/bin/update-config && \
    chown -R elasticsearch:elasticsearch /app && \
    mkdir -p /var/log/supervisor >/dev/null 2>&1 && \
    chown -R elasticsearch:elasticsearch /var/log/supervisor

USER elasticsearch

RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --channel 8.0 --quality ga --runtime aspnetcore && \
    rm dotnet-install.sh

EXPOSE 8080 9200

ENTRYPOINT ["/app/docker-entrypoint.sh"]

# build locally
# docker buildx build --target exceptionless --platform linux/amd64,linux/arm64 --load --tag exceptionless .
