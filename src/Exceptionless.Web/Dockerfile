FROM microsoft/dotnet:2.1-runtime-deps
LABEL Author="Exceptionless"
COPY published/ .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000/tcp
ENTRYPOINT [ "./Exceptionless.Web" ]
