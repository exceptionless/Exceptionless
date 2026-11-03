---
title: "Docker"
metaTitle: "Self-hosting Exceptionless with Docker Compose"
description: "Run self-hosted Exceptionless with Docker Compose, or try it in one container. Covers persistent data, email, and HTTPS."
sources:
  - Dockerfile
  - samples/docker-compose.yml
  - samples/docker-compose.all-in-one.yml
  - samples/hosting-samples.sh
  - build/docker-entrypoint.sh
  - build/update-config.sh
  - src/Exceptionless.Web/Program.cs
  - src/Exceptionless.Core/Configuration/CustomEnvironmentVariablesConfiguration.cs
  - src/Exceptionless.Core/Configuration/EmailOptions.cs
  - src/Exceptionless.Core/Utility/SmtpUri.cs
  - src/Exceptionless.Core/Configuration/ElasticsearchOptions.cs
---

# Docker

This guide runs Exceptionless with Docker Compose: the app, the background jobs, Elasticsearch, and Redis, each in its
own container, with data kept between restarts. To look around first, you can also
[run everything in one container](#try-it-with-one-container).

You need [Docker](https://docs.docker.com/get-started/get-docker/) with Docker Compose.

## Run Exceptionless with Docker Compose

1. Save this file as `compose.yaml` in an empty folder:

   ```yaml
   # Settings shared by the app and the jobs.
   x-exceptionless-config: &exceptionless-config
     EX_AppMode: Production
     # The public URL of the web app. Used in email links, OAuth, and the MCP server.
     EX_BaseURL: http://localhost:7110
     # The server URL shown in the app's client setup instructions.
     EX_ExceptionlessServerUrl: http://localhost:7110
     EX_ConnectionStrings__Elasticsearch: server=http://elasticsearch:9200;replicas=0
     EX_ConnectionStrings__Redis: redis,abortConnect=false
     EX_ConnectionStrings__Storage: provider=folder;path=/app/storage
     EX_RunJobsInProcess: "false"
     # Uncomment to send email.
     # EX_ConnectionStrings__Email: smtp://user:password@smtp.example.com:587
     # EX_SmtpFrom: Exceptionless <noreply@example.com>

   services:
     app:
       image: exceptionless/app:latest
       environment: *exceptionless-config
       ports:
         - 7110:8080
       volumes:
         - ex_storage:/app/storage
       depends_on:
         elasticsearch:
           condition: service_healthy
         redis:
           condition: service_started
       restart: unless-stopped

     jobs:
       image: exceptionless/job:latest
       environment: *exceptionless-config
       volumes:
         - ex_storage:/app/storage
       depends_on:
         app:
           condition: service_started
       restart: unless-stopped

     elasticsearch:
       image: exceptionless/elasticsearch:8.19.22
       environment:
         discovery.type: single-node
         xpack.security.enabled: "false"
         ES_JAVA_OPTS: -Xms1g -Xmx1g
       volumes:
         - ex_esdata:/usr/share/elasticsearch/data
       healthcheck:
         test: [
           "CMD-SHELL",
           "curl -fsS 'http://localhost:9200/_cluster/health?wait_for_status=yellow&timeout=1s' > /dev/null",
         ]
         interval: 10s
         timeout: 10s
         retries: 30
         start_period: 20s
       restart: unless-stopped

     redis:
       image: redis:7.4-alpine
       volumes:
         - ex_redisdata:/data
       restart: unless-stopped

   volumes:
     ex_esdata:
     ex_storage:
     ex_redisdata:
   ```

2. Start it:

   ```bash
   docker compose up -d
   ```

   The first start takes a minute or two while Elasticsearch starts and Exceptionless creates its indexes. The jobs
   container runs any pending migrations each time it starts.

3. Open `http://localhost:7110` and sign up. The first account on a new instance is a global administrator.

4. Create an organization and a project, then [send your first event](/docs/getting-started/). Point your client at your
   server, for example `serverUrl: "http://localhost:7110"` in JavaScript or `ServerUrl` in .NET.

What the file sets up:

- The `app` container serves the web UI and the API on port 7110. `EX_RunJobsInProcess: "false"` leaves the background
  work to the `jobs` container.
- The `jobs` container runs every background job. It shares the `ex_storage` volume with the app, because the app saves
  incoming events there before the jobs process them.
- Redis carries the cache, queues, and messages between the two. Setting only `EX_ConnectionStrings__Redis` is enough;
  the cache, queue, and message bus use it automatically.
- `replicas=0` creates indexes without replicas, because a single Elasticsearch node can't hold them.

To see the logs, run `docker compose logs -f app jobs`.

## Use your own domain

When you serve Exceptionless at a real host name, change `EX_BaseURL` and `EX_ExceptionlessServerUrl` to that URL, for
example `https://errors.example.com`, and run `docker compose up -d` again.

Put a reverse proxy such as nginx or Caddy in front of the app to handle HTTPS, and tell Exceptionless to trust it. See
[Reverse proxies and HTTPS](/docs/self-hosting/reverse-proxy/). Every setting you can add to the shared block is in the
[configuration reference](/docs/self-hosting/configuration/).

## Send email

Exceptionless sends email for sign-up verification, password resets, invites, notifications, and daily summaries. Add an
SMTP connection string and a from address to the shared settings:

```yaml
EX_ConnectionStrings__Email: smtp://user:password@smtp.example.com:587
EX_SmtpFrom: Exceptionless <noreply@example.com>
```

- Always include the port. Exceptionless picks the encryption from it: SSL for 465, STARTTLS for 587 and 2525, and none
  for other ports. To choose yourself, set `EX_SmtpEncryption` to `None`, `StartTLS`, or `SSL`.
- Percent-encode reserved characters in the user name and password. For example, `@` becomes `%40`, `#` becomes `%23`,
  and `+` becomes `%2B`.
- Use a from address that your SMTP provider allows you to send from. In `Production` mode there is no default, and
  email fails without one.

## Try it with one container {#try-it-with-one-container}

The `exceptionless/exceptionless` image runs the app, the jobs, and Elasticsearch in one container, with in-memory
queues and cache. It's good for a first look and for testing, not for production.

This command keeps nothing when the container stops:

```bash
docker run --rm -it -p 7110:8080 \
  -e EX_BaseURL=http://localhost:7110 \
  exceptionless/exceptionless:latest
```

Open `http://localhost:7110` once the log shows the app has started.

To keep your data between runs, store Elasticsearch's data in a named volume:

```bash
docker run --rm -it -p 7110:8080 \
  -e EX_BaseURL=http://localhost:7110 \
  -v ex_esdata:/usr/share/elasticsearch/data \
  exceptionless/exceptionless:latest
```

In PowerShell, end each line with a backtick (`` ` ``) instead of a backslash.

## Add HTTPS and email to one container {#simple-setup-wssl-support-and-smtp}

The app can serve HTTPS itself with a certificate file. This example serves HTTP on port 7110 and HTTPS on port 7111,
and sends email through SMTP.

1. Create a certificate. For a local test, export the .NET development certificate:

   ```bash
   dotnet dev-certs https -ep ~/.aspnet/https/aspnetapp.pfx -p password
   ```

   For a real server, use a certificate for your host name in PFX format. The container runs as a non-root user, so the
   file must be readable by others.

2. Run the container:

   ```bash
   docker run --rm -it -p 7110:8080 -p 7111:8443 \
     -e ASPNETCORE_URLS="http://+:8080;https://+:8443" \
     -e EX_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx \
     -e EX_Kestrel__Certificates__Default__Password=password \
     -e EX_BaseURL=https://localhost:7111 \
     -e EX_ConnectionStrings__Email=smtp://user:password@smtp.example.com:587 \
     -e "EX_SmtpFrom=Exceptionless <noreply@example.com>" \
     -v ~/.aspnet/https:/https:ro \
     -v ex_esdata:/usr/share/elasticsearch/data \
     exceptionless/exceptionless:latest
   ```

   - `ASPNETCORE_URLS` makes the app listen for HTTP on port 8080 and HTTPS on port 8443 inside the container. The `-p`
     options publish them as 7110 and 7111.
   - The certificate settings use the `EX_` prefix. Exceptionless doesn't read `ASPNETCORE_Kestrel__...` variables.
   - Exceptionless doesn't redirect HTTP to HTTPS, so port 7110 keeps serving plain HTTP. To serve only HTTPS, leave out
     `-p 7110:8080`.

3. Open `https://localhost:7111`.

The same `ASPNETCORE_URLS` and certificate settings work on the `app` container in the Compose file. Most production
setups terminate HTTPS at a [reverse proxy](/docs/self-hosting/reverse-proxy/) instead.

## Upgrade

Pull the new images and recreate the containers:

```bash
docker compose pull
docker compose up -d
```

Migrations run when the jobs container starts. See [Upgrading](/docs/self-hosting/upgrading-self-hosted-instance/) for
what to check first.

## Set it up with a coding agent

```prompt agent
Read https://exceptionless.com/docs/self-hosting/docker.md and https://exceptionless.com/docs/self-hosting/reverse-proxy.md. Write a compose.yaml that runs self-hosted Exceptionless at https://errors.example.com behind Caddy, with SMTP email through smtp.example.com on port 587. Trust only the Caddy container as a proxy.
```
