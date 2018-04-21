FROM microsoft/dotnet:2.1-runtime
LABEL Author="Exceptionless"
COPY published/ .

ENTRYPOINT ["dotnet", "Exceptionless.Job.dll"]
