FROM microsoft/dotnet:2.1-sdk AS build  
WORKDIR /app

COPY ./*.sln ./NuGet.Config ./
COPY ./build/*.props ./build/

# Copy the main source project files
COPY src/*/*.csproj ./
RUN for file in $(ls *.csproj); do mkdir -p src/${file%.*}/ && mv $file src/${file%.*}/; done

# Copy the individual jobs (temporary)
COPY src/Jobs/*/*.csproj ./
RUN for file in $(ls *.csproj); do mkdir -p src/Jobs/${file%.*}/ && mv $file src/Jobs/${file%.*}/; done

# Copy the test project files
COPY tests/*/*.csproj ./
RUN for file in $(ls *.csproj); do mkdir -p tests/${file%.*}/ && mv $file tests/${file%.*}/; done

RUN dotnet restore

# Copy everything else and build app
COPY . .
RUN dotnet build

# testrunner

FROM build AS testrunner
WORKDIR /app/tests/Exceptionless.Tests
ENTRYPOINT [ "dotnet", "test", "--verbosity", "minimal", "--logger:trx" ]

# job-publish

FROM build AS job-publish
WORKDIR /app/src/Exceptionless.Job
RUN dotnet publish -c Release -o out

# job

FROM microsoft/dotnet:2.1-runtime AS job
WORKDIR /app
COPY --from=job-publish /app/src/Exceptionless.Job/out ./
ENTRYPOINT [ "dotnet", "Exceptionless.Job.dll" ]


# api-publish

FROM build AS api-publish
WORKDIR /app/src/Exceptionless.Web
RUN dotnet publish -c Release -o out

# api

FROM microsoft/dotnet:2.1-aspnetcore-runtime AS api
WORKDIR /app
COPY --from=api-publish /app/src/Exceptionless.Web/out ./
ENTRYPOINT [ "dotnet", "Exceptionless.Web.dll" ]
