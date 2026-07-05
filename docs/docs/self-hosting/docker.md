---
title: "Docker"
---

# Docker

If you would like to test Exceptionless locally, please follow this section.

## Requirements

* [Docker](https://www.docker.com)

## Testing Setup

Runs Exceptionless without persisting data between runs. Good for checking out Exceptionless for the first time and testing.

```bash
docker run --rm -it -p 5200:8080 exceptionless/exceptionless:latest
```

## Simple Setup

Runs a very simple non-production setup for Exceptionless with data persisted between runs in a sub-directory of the current directory called `esdata`. It uses an embedded single node Elasticsearch cluster and does not have backups. It is recommended that you create your own Elasticsearch cluster for production deployments of Exceptionless.

On Linux:

```bash
docker run --rm -it -p 5200:8080 \
    -v $(pwd)/esdata:/usr/share/elasticsearch/data \
    exceptionless/exceptionless:latest
```

On PowerShell:

```powershell
docker run --rm -it -p 5200:8080 `
    -v ${PWD}/esdata:/usr/share/elasticsearch/data `
    exceptionless/exceptionless:latest
```

## Simple Setup w/SSL Support and SMTP

Runs a very simple non-production setup for Exceptionless with data persisted between runs in a sub-directory of the current directory called `esdata`. It uses an embedded single node Elasticsearch cluster and does not have backups. It is recommended that you create your own Elasticsearch cluster for production deployments of Exceptionless. In the SMTP password characters disallowed or reserved according to RFC-2396 (e.g. @:#/?+) need to be percent-encoded (e.g. # => %23).

On Linux:

```bash
docker run --rm -it -p 5200:8080 -p 5089:443 \
    -e EX_ConnectionStrings__Email=smtps://user:password@smtp.host.com:587 \
    -e ASPNETCORE_URLS="https://+;http://+" \
    -e ASPNETCORE_HTTPS_PORT=5001 \
    -e ASPNETCORE_Kestrel__Certificates__Default__Password="password" \
    -e ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx \
    -v ~/.aspnet/https:/https/ \
    -v $(PWD)/esdata:/usr/share/elasticsearch/data \
    exceptionless/exceptionless:latest
```

On PowerShell:

```powershell
docker run --rm -it -p 5200:8080 -p 5089:443 `
    -e EX_ConnectionStrings__Email=smtps://user:password@smtp.host.com:587 `
    -e ASPNETCORE_URLS="https://+;http://+" `
    -e ASPNETCORE_HTTPS_PORT=5001 `
    -e ASPNETCORE_Kestrel__Certificates__Default__Password="password" `
    -e ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx `
    -v ~/.aspnet/https:/https/ `
    -v ${PWD}/esdata:/usr/share/elasticsearch/data `
    exceptionless/exceptionless:latest
```

---

[Next > Kubernetes](/docs/self-hosting/kubernetes)
