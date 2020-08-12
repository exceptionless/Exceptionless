ARG UI_VERSION=ui:2.9.2
FROM exceptionless/${UI_VERSION} AS ui

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /app

COPY ./*.sln ./NuGet.Config ./
COPY ./build/*.props ./build/

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

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS job
WORKDIR /app
COPY --from=job-publish /app/src/Exceptionless.Job/out ./
ENTRYPOINT [ "dotnet", "Exceptionless.Job.dll" ]

# api-publish

FROM build AS api-publish
WORKDIR /app/src/Exceptionless.Web

RUN dotnet publish -c Release -o out

# api

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS api
WORKDIR /app
COPY --from=api-publish /app/src/Exceptionless.Web/out ./
ENTRYPOINT [ "dotnet", "Exceptionless.Web.dll" ]

# app

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS app

WORKDIR /app
COPY --from=api-publish /app/src/Exceptionless.Web/out ./
COPY --from=ui /app ./wwwroot
COPY --from=ui /usr/local/bin/bootstrap /usr/local/bin/bootstrap
COPY ./build/docker-entrypoint.sh ./
COPY ./build/supervisord.conf /etc/

ENV EX_ConnectionStrings__Storage=provider=folder;path=/app/storage \
    EX_RunJobsInProcess=true \
    ASPNETCORE_URLS=http://+:80 \
    EX_Html5Mode=true

EXPOSE 80

ENTRYPOINT ["/app/docker-entrypoint.sh"]
CMD [ "dotnet", "Exceptionless.Web.dll" ]

# completely self-contained

FROM exceptionless/elasticsearch:7.8.1 AS exceptionless

WORKDIR /app
COPY --from=api-publish /app/src/Exceptionless.Web/out ./
COPY --from=ui /app ./wwwroot
COPY --from=ui /usr/local/bin/bootstrap /usr/local/bin/bootstrap
COPY ./build/docker-entrypoint.sh ./
COPY ./build/supervisord.conf /etc/

# install dotnet and supervisor
RUN rpm -Uvh https://packages.microsoft.com/config/centos/7/packages-microsoft-prod.rpm && \
    yum -y install aspnetcore-runtime-3.1 && \
    yum -y install epel-release && \
    yum -y install supervisor

ENV discovery.type=single-node \
    xpack.security.enabled=false \
    ES_JAVA_OPTS="-Xms1g -Xmx1g" \
    ASPNETCORE_URLS=http://+:80 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    EX_ConnectionStrings__Storage=provider=folder;path=/app/storage \
    EX_RunJobsInProcess=true \
    EX_Html5Mode=true

EXPOSE 80 9200

ENTRYPOINT ["/app/docker-entrypoint.sh"]
