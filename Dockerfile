FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-node
RUN apt-get update -yq \
    && apt-get install -yq curl ca-certificates \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -yq nodejs \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG MinVerVersionOverride
WORKDIR /app

COPY ./NuGet.Config ./
COPY ./src/*.props ./src/
COPY ./build/packages/* ./build/packages/

# Copy the source project files needed for the runtime images.
COPY src/*/*.csproj ./
RUN for file in $(ls *.csproj); do mkdir -p src/${file%.*}/ && mv $file src/${file%.*}/; done

RUN dotnet restore ./src/Exceptionless.Web/Exceptionless.Web.csproj \
    && dotnet restore ./src/Exceptionless.Job/Exceptionless.Job.csproj

# Copy everything else and build the source projects once.
COPY . .
RUN dotnet build ./src/Exceptionless.Web/Exceptionless.Web.csproj -c Release --no-restore /p:MinVerVersionOverride=${MinVerVersionOverride} \
    && dotnet build ./src/Exceptionless.Job/Exceptionless.Job.csproj -c Release --no-restore /p:MinVerVersionOverride=${MinVerVersionOverride}

# testrunner

FROM build AS testrunner
WORKDIR /app/tests/Exceptionless.Tests
ENTRYPOINT ["dotnet", "test", "--results-directory", "/app/artifacts", "--logger:trx"]

# job-publish

FROM build AS job-publish
WORKDIR /app/src/Exceptionless.Job

RUN dotnet publish -c Release -o out --no-build

# job

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS job
WORKDIR /app
COPY --from=job-publish /app/src/Exceptionless.Job/out ./

EXPOSE 8080

ENTRYPOINT [ "dotnet", "Exceptionless.Job.dll" ]

# api-publish

FROM build AS api-publish
WORKDIR /app/src/Exceptionless.Web

RUN dotnet publish -c Release -o out --no-build /p:SkipSpaPublish=true

# api

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=api-publish /app/src/Exceptionless.Web/out ./

EXPOSE 8080

ENTRYPOINT [ "dotnet", "Exceptionless.Web.dll" ]

# app-publish

FROM build-node AS app-publish
WORKDIR /app
COPY --from=build /app ./

WORKDIR /app/src/Exceptionless.Web
RUN dotnet publish -c Release -o out --no-build

# app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS app

WORKDIR /app
COPY --from=app-publish /app/src/Exceptionless.Web/out ./
COPY ./build/app-docker-entrypoint.sh ./
COPY ./build/update-config.sh /usr/local/bin/update-config
COPY ./build/update-config-next.sh /usr/local/bin/update-config-next

ENV EX_ConnectionStrings__Storage=provider=folder;path=/app/storage \
    EX_RunJobsInProcess=true \
    ASPNETCORE_URLS=http://+:8080 \
    EX_Html5Mode=true

RUN chmod +x /app/app-docker-entrypoint.sh
RUN chmod +x /usr/local/bin/update-config
RUN chmod +x /usr/local/bin/update-config-next

EXPOSE 8080

ENTRYPOINT ["/app/app-docker-entrypoint.sh"]

# completely self-contained

FROM exceptionless/elasticsearch:8.19.14 AS exceptionless

WORKDIR /app
COPY --from=job-publish /app/src/Exceptionless.Job/out ./
COPY --from=app-publish /app/src/Exceptionless.Web/out ./
COPY ./build/docker-entrypoint.sh ./
COPY ./build/update-config.sh /usr/local/bin/update-config
COPY ./build/update-config-next.sh /usr/local/bin/update-config-next
COPY ./build/supervisord.conf /etc/

USER root

# install dotnet and supervisor
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
    supervisor \
    wget \
    dos2unix \
    ca-certificates \
    \
    # .NET dependencies
    libc6 \
    libgcc-s1 \
    libicu74 \
    libssl3 \
    libstdc++6 \
    tzdata \
    && rm -rf /var/lib/apt/lists/* \
    && dos2unix /app/docker-entrypoint.sh

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
    chmod +x /usr/local/bin/update-config-next && \
    chown -R elasticsearch:elasticsearch /app && \
    mkdir -p /var/log/supervisor >/dev/null 2>&1 && \
    chown -R elasticsearch:elasticsearch /var/log/supervisor

USER elasticsearch

RUN wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --channel 10.0 --runtime aspnetcore && \
    rm dotnet-install.sh

EXPOSE 8080 9200

ENTRYPOINT ["/app/docker-entrypoint.sh"]

# build locally
# docker buildx build --target exceptionless --platform linux/amd64,linux/arm64 --load --tag exceptionless .
