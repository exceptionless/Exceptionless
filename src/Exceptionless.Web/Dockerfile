FROM microsoft/dotnet:2.0-runtime-deps-jessie
LABEL Author="Exceptionless"
COPY published/ .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000/tcp
ENTRYPOINT [ "./Exceptionless.Web" ]
