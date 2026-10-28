---
title: "Configuration"
metaTitle: "Self-hosted Exceptionless configuration reference"
description: "Every self-hosted Exceptionless setting: EX_ environment variables, connection strings, email, storage, sign-in providers, OAuth, and AI."
sources:
  - src/Exceptionless.Web/Program.cs
  - src/Exceptionless.Web/appsettings.yml
  - src/Exceptionless.Web/appsettings.Production.yml
  - src/Exceptionless.Core/Configuration/CustomEnvironmentVariablesConfiguration.cs
  - src/Exceptionless.Core/Configuration/AppOptions.cs
  - src/Exceptionless.Core/Configuration/AuthOptions.cs
  - src/Exceptionless.Core/Configuration/AssistantOptions.cs
  - src/Exceptionless.Core/Configuration/CacheOptions.cs
  - src/Exceptionless.Core/Configuration/ElasticsearchOptions.cs
  - src/Exceptionless.Core/Configuration/EmailOptions.cs
  - src/Exceptionless.Core/Configuration/MessageBusOptions.cs
  - src/Exceptionless.Core/Configuration/OAuthServerOptions.cs
  - src/Exceptionless.Core/Configuration/QueueOptions.cs
  - src/Exceptionless.Core/Configuration/SlackOptions.cs
  - src/Exceptionless.Core/Configuration/SourceMapOptions.cs
  - src/Exceptionless.Core/Configuration/StorageOptions.cs
  - src/Exceptionless.Core/Configuration/StripeOptions.cs
  - src/Exceptionless.Core/Authentication/ActiveDirectoryLoginProvider.cs
  - src/Exceptionless.Core/Utility/SmtpUri.cs
  - src/Exceptionless.Insulation/Bootstrapper.cs
  - src/Exceptionless.Web/Mcp/McpOriginValidationMiddleware.cs
  - src/Exceptionless.Web/Security/TrustedForwardedHeadersOptions.cs
  - src/Exceptionless.Web/ClientApp/src/lib/features/auth/index.svelte.ts
  - build/update-config.sh
---

# Configuration

This page lists the settings for a self-hosted Exceptionless instance, with the environment variable for each, its
default, and what it does. Give the app, API, and job containers the same settings.

## How settings are read

Exceptionless reads settings from these sources. Later sources override earlier ones:

1. `appsettings.yml`, then `appsettings.<AppMode>.yml`, then `appsettings.Local.yml`, from the app folder (`/app` in the
   containers).
2. Environment variables.
3. Command-line arguments.

Environment variable names follow these rules:

- The `EX_` prefix is optional and is removed when the variable is read. Use it to keep Exceptionless settings apart
  from others.
- Double underscores (`__`) separate nested keys: `EX_ConnectionStrings__Redis` sets `ConnectionStrings:Redis`.
- Lists use numbered keys: `EX_Mcp__AllowedOrigins__0`, `EX_Mcp__AllowedOrigins__1`. A few settings, noted below, take a
  comma-separated list instead.
- Server-side names aren't case-sensitive.

The `app` and all-in-one images also copy some settings into the web UI when the container starts. The UI reads only
environment variables with these exact names: `EX_ApiUrl`, `EX_EnableAccountCreation`, `EX_EnableSsl`,
`EX_NotificationMessage`, `EX_ExceptionlessApiKey`, `EX_ExceptionlessServerUrl`, `EX_ClientSetupShowServerUrl`,
`EX_StripePublishableApiKey`, and the client IDs in `EX_ConnectionStrings__OAuth`. Set them as environment variables,
not in YAML, and restart the container after you change them.

> [!IMPORTANT]
> Always set `EX_BaseURL`. The built-in production settings point at the hosted service, `https://be.exceptionless.io`.

## App

| Environment variable          | Default                                      | What it does                                                                                                                                                                                                 |
| ----------------------------- | -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `EX_BaseURL`                  | The hosted service; always set it            | The public URL of the web app, without a trailing slash, for example `https://errors.example.com`. Used in email links, OAuth sign-in, and the MCP server.                                                   |
| `EX_ApiUrl`                   | `EX_BaseURL`                                 | The public URL of the API, if it's on a different host name than the web app. The web UI sends its API calls there, and OAuth and MCP discovery use it.                                                      |
| `EX_AppMode`                  | `Production`                                 | `Production`, `Staging`, or `Development`. Picks `appsettings.<AppMode>.yml` and changes several defaults below. Set it as an environment variable.                                                          |
| `EX_AppScope`                 | `prod`, `stage`, or `dev`, from `EX_AppMode` | A prefix for index, queue, cache, and storage names, so several instances can share Elasticsearch and Redis. Changing it, or changing `EX_AppMode`, on an existing instance points it at new, empty indexes. |
| `EX_RunJobsInProcess`         | `true` in `Development`, otherwise `false`   | Runs the background jobs inside the web process. The `app` and all-in-one images set it to `true`. Set it to `false` when you run the `job` image, which also needs Redis.                                   |
| `EX_EventSubmissionDisabled`  | `false`                                      | Rejects new events with HTTP 503. A global administrator can override it at runtime under **System** > **Settings**.                                                                                         |
| `EX_MaximumEventPostSize`     | `200000`                                     | The largest event submission request body, in bytes.                                                                                                                                                         |
| `EX_MaximumRetentionDays`     | `180`                                        | The most days events are kept, for every organization. Organizations on the Unlimited plan keep events this long.                                                                                            |
| `EX_ApiThrottleLimit`         | `3500`; unlimited in `Development`           | API requests allowed in 15 minutes for each organization API key, user, or anonymous client IP address.                                                                                                      |
| `EX_BotThrottleLimit`         | `25`                                         | For projects that delete bot data, the events from one public client IP address in five minutes that mark it as a bot. Its events in that window are removed.                                                |
| `EX_EnableWebSockets`         | `true`                                       | Sends real-time updates to the web UI over a WebSocket at `/api/v2/push`.                                                                                                                                    |
| `EX_EnableSsl`                | `false`                                      | Moves the web UI from `http://` to `https://` on the same host and port, and tells browsers to upgrade insecure requests. Use it when HTTPS is served on the same host name, such as behind a reverse proxy. |
| `EX_EnableArchive`            | `false`                                      | Keeps a copy of every raw event submission in storage.                                                                                                                                                       |
| `EX_NotificationMessage`      | None                                         | A banner message for every user of the web UI. A notification set under **System** > **Notifications** takes its place.                                                                                      |
| `EX_DisabledPlugins`          | None                                         | A comma-separated list of event processing plugins to turn off, by class name, such as `GeoPlugin,LocationPlugin`.                                                                                           |
| `EX_MaxMindGeoIpKey`          | None                                         | A MaxMind license key. A daily job downloads the GeoLite2 City database and uses it to look up locations from IP addresses.                                                                                  |
| `EX_GoogleGeocodingApiKey`    | None                                         | A Google Geocoding API key used to resolve locations.                                                                                                                                                        |
| `EX_ExceptionlessApiKey`      | None                                         | An API key for an Exceptionless project that receives this instance's own errors and logs. The web UI reports its errors there too.                                                                          |
| `EX_ExceptionlessServerUrl`   | None                                         | The server URL shown in the app's client setup instructions. With `EX_ExceptionlessApiKey`, also where this instance sends its own errors; without it, they go to the hosted service in `Production`.        |
| `EX_InternalProjectId`        | The hosted service's project                 | The ID of the project that receives this instance's own errors, so they aren't processed in a loop.                                                                                                          |
| `EX_ClientSetupShowServerUrl` | `true`                                       | Set to `false` to leave the server URL out of the client setup instructions.                                                                                                                                 |

## Connection strings

Connection strings use `key=value` pairs separated by semicolons. The Cache, Queue, MessageBus, and Storage connection
strings name a `provider`. If another connection string has the provider's name, its values are added, so
`provider=redis` reuses `EX_ConnectionStrings__Redis`.

| Environment variable                  | Example                                     | What it does                                                                                                                                                                                         |
| ------------------------------------- | ------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `EX_ConnectionStrings__Elasticsearch` | `server=http://elasticsearch:9200`          | Elasticsearch. See [Elasticsearch options](#elasticsearch-options).                                                                                                                                  |
| `EX_ConnectionStrings__Redis`         | `redis:6379,abortConnect=false`             | Redis, in [StackExchange.Redis format](https://stackexchange.github.io/StackExchange.Redis/Configuration). When set, the cache, queues, and message bus use it unless you configure them separately. |
| `EX_ConnectionStrings__Cache`         | `provider=redis`                            | The shared cache. `redis` is the only provider. Without Redis, each process keeps its own in-memory cache.                                                                                           |
| `EX_ConnectionStrings__Queue`         | `provider=redis`                            | The work queues. See [Queues](#queues).                                                                                                                                                              |
| `EX_ConnectionStrings__MessageBus`    | `provider=redis`                            | The message bus between processes: `redis` or `rabbitmq`.                                                                                                                                            |
| `EX_ConnectionStrings__Storage`       | `provider=folder;path=/app/storage`         | File storage. See [Storage](#storage). The `app` and all-in-one images default to this example; without it, files are kept in memory.                                                                |
| `EX_ConnectionStrings__Email`         | `smtp://user:password@smtp.example.com:587` | The SMTP server. See [Email](#email).                                                                                                                                                                |
| `EX_ConnectionStrings__OAuth`         | `GitHubId=...;GitHubSecret=...`             | Sign-in providers and Slack. See [Sign-in providers](#sign-in-providers).                                                                                                                            |
| `EX_ConnectionStrings__LDAP`          | `LDAP://ad.example.com/DC=example,DC=com`   | Active Directory sign-in. See [Active Directory](#active-directory).                                                                                                                                 |

### Elasticsearch options {#elasticsearch-options}

Add these keys to the Elasticsearch connection string, for example
`server=https://es.example.com:9200;username=exceptionless;password=secret;replicas=1`.

| Key                  | Default                            | What it does                                                                             |
| -------------------- | ---------------------------------- | ---------------------------------------------------------------------------------------- |
| `server`             | `http://localhost:9200`            | The Elasticsearch URL. A connection string with only a URL uses it as the server.        |
| `username`           | None                               | The user name for basic authentication.                                                  |
| `password`           | None                               | The password for basic authentication.                                                   |
| `shards`             | `1`                                | Primary shards for new indexes.                                                          |
| `replicas`           | `1` in `Production`, otherwise `0` | Replicas for new indexes. Use `0` on a single-node cluster.                              |
| `field-limit`        | `1500`                             | The mapping field limit for event indexes.                                               |
| `enable-size-plugin` | `true`, except in `Development`    | Uses the `mapper-size` plugin. Set to `false` if your cluster doesn't have it installed. |
| `scope`              | `EX_AppScope`                      | Overrides the index name prefix for Elasticsearch only.                                  |

### Queues {#queues}

| Provider       | Keys                                 | Notes                                                                                                                                                                                                                                                                                                                               |
| -------------- | ------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `redis`        | Reuses `EX_ConnectionStrings__Redis` | The usual choice.                                                                                                                                                                                                                                                                                                                   |
| `sqs`          | `accesskey`, `secretkey`, `region`   | Amazon SQS. Without keys, the default AWS credential chain is used. The region defaults to `us-east-1`. Exceptionless doesn't create SQS queues; create `EventPost`, `EventUserDescription`, `EventNotification`, `WebHookNotification`, `MailMessage`, and `WorkItemData`, each prefixed with the scope, such as `prod-EventPost`. |
| `azurestorage` | An Azure Storage connection string   | Azure Storage queues. You can also set `EX_ConnectionStrings__AzureQueues` to an Azure Storage connection string instead.                                                                                                                                                                                                           |

Without a queue provider, queues are kept in memory and work only when everything runs in one process.

### Storage {#storage}

| Provider       | Keys                                         | Notes                                                                                                                                                                          |
| -------------- | -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `folder`       | `path`                                       | A folder on disk. When the app and jobs run in separate containers, they must share it.                                                                                        |
| `s3`           | `bucket`, `region`, `accesskey`, `secretkey` | Amazon S3. The bucket defaults to the scope plus `ex-events`, such as `prod-ex-events`, and the region to `us-east-1`. Without keys, the default AWS credential chain is used. |
| `azurestorage` | An Azure Storage connection string           | Azure Blob Storage, in a container named with the scope plus `ex-events`. You can also set `EX_ConnectionStrings__AzureStorage` to an Azure Storage connection string instead. |
| `aliyun`       | An Alibaba Cloud OSS connection string       | Alibaba Cloud Object Storage Service.                                                                                                                                          |

## Email {#email}

| Environment variable          | Default                                                    | What it does                                                                                                                            |
| ----------------------------- | ---------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| `EX_ConnectionStrings__Email` | None                                                       | The SMTP server, as `smtp://user:password@host:port`. Without it, no email is sent.                                                     |
| `EX_SmtpFrom`                 | None; `Exceptionless <noreply@localhost>` in `Development` | The from address, such as `Exceptionless <noreply@example.com>`. Required to send email.                                                |
| `EX_SmtpEncryption`           | From the port                                              | `None`, `StartTLS`, or `SSL`. By default, port 465 uses SSL, ports 587 and 2525 use STARTTLS, and other ports use none.                 |
| `EX_EnableDailySummary`       | `true` in `Production`, otherwise `false`                  | Sends daily summary emails to users who turn them on.                                                                                   |
| `EX_AllowedOutboundAddresses` | None                                                       | Outside `Production`, a comma-separated list of addresses or domains that may receive email. Other email goes to `EX_TestEmailAddress`. |
| `EX_TestEmailAddress`         | None; `admin@exceptionless.test` in `Development`          | Outside `Production`, receives email meant for addresses not in `EX_AllowedOutboundAddresses`. Without it, that email is dropped.       |
| `EX_ContactEmailAddress`      | None                                                       | Receives messages from the contact form.                                                                                                |

Include the port in the SMTP connection string; `smtp://` and `smtps://` behave the same, and the port picks the
encryption. Percent-encode reserved characters in the user name and password, such as `@` as `%40` and `+` as `%2B`. If
you set a user name, you must also set a password.

## Accounts and sign-in

| Environment variable           | Default                         | What it does                                                                                                                                      |
| ------------------------------ | ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| `EX_EnableAccountCreation`     | `true`                          | When `false`, people can create an account only from an organization invite. This applies to email and password sign-up and to sign-in providers. |
| `EX_ConnectionStrings__OAuth`  | None                            | Client IDs and secrets for sign-in providers and Slack, as `key=value` pairs.                                                                     |
| `EX_ConnectionStrings__LDAP`   | None                            | The LDAP path for Active Directory sign-in.                                                                                                       |
| `EX_EnableActiveDirectoryAuth` | `true` when an LDAP path is set | Uses Active Directory to check passwords.                                                                                                         |

The first user who signs up on a new instance becomes a global administrator.

### Sign-in providers {#sign-in-providers}

Users can sign in with Microsoft, GitHub, or Google once you add that provider's keys to `EX_ConnectionStrings__OAuth`.
Set it as an environment variable on the `app` or all-in-one container, so the web UI shows the matching buttons:

```bash
EX_ConnectionStrings__OAuth=MicrosoftId=YOUR_ID;MicrosoftSecret=YOUR_SECRET;GitHubId=YOUR_ID;GitHubSecret=YOUR_SECRET;GoogleId=YOUR_ID;GoogleSecret=YOUR_SECRET
```

Include only the providers you use. Each provider sends users back to the origin of your web app, such as
`https://errors.example.com`, so register that exact URL as the redirect URI.

| Provider  | Keys                             | Where to register                                                                                                                                                                                                                       |
| --------- | -------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Microsoft | `MicrosoftId`, `MicrosoftSecret` | An app registration in Microsoft Entra with a **Web** redirect URI and a client secret. Sign-in uses the `common` endpoint and the `User.Read` scope, so choose an account type that includes accounts in any organizational directory. |
| GitHub    | `GitHubId`, `GitHubSecret`       | A GitHub OAuth app. Set its authorization callback URL to your web app's origin. Exceptionless asks for the `user:email` scope.                                                                                                         |
| Google    | `GoogleId`, `GoogleSecret`       | An OAuth client ID of type **Web application** in Google Cloud, with your web app's origin as an authorized redirect URI. Exceptionless asks for `openid profile email`.                                                                |

### Slack

Slack notifications use the same connection string, with `SlackId` and `SlackSecret` from a Slack app:

1. [Create a Slack app](https://api.slack.com/apps) in your workspace. Don't distribute it outside your organization.
2. Under **OAuth & Permissions**, add your web app's origin, such as `https://errors.example.com`, as a redirect URL.
3. Copy the client ID and client secret from **Basic Information** into `SlackId=...;SlackSecret=...`.
4. Restart the containers. Users can then connect Slack from their project's integrations.

### Active Directory {#active-directory}

With `EX_ConnectionStrings__LDAP` set, Exceptionless checks passwords against Active Directory instead of storing them.

- Set the value to your domain's LDAP path, such as `LDAP://ad.example.com/DC=example,DC=com`. To narrow the search, use
  a more specific path, such as `LDAP://ad.example.com/OU=Users,DC=example,DC=com`.
- Users still sign up once with their directory email address and password. Exceptionless finds the account by its
  `mail` attribute and signs in with its `sAMAccountName`.
- Active Directory sign-in uses Windows directory APIs, so it works only when Exceptionless runs on Windows. The Docker
  images run on Linux and can't use it.

## OAuth server and MCP {#oauth-server-and-mcp}

Exceptionless is an OAuth server for the [MCP server](/docs/ai/mcp-server/) and for apps that call the API with OAuth
tokens. See [AI features](/docs/self-hosting/ai/) for how these fit together.

| Environment variable                                          | Default | What it does                                                                                                                                                                             |
| ------------------------------------------------------------- | ------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `EX_OAuthServer__AuthorizationCodeLifetimeMinutes`            | `5`     | How long an authorization code is valid.                                                                                                                                                 |
| `EX_OAuthServer__AccessTokenLifetimeMinutes`                  | `60`    | How long an access token is valid.                                                                                                                                                       |
| `EX_OAuthServer__RefreshTokenLifetimeDays`                    | `30`    | How long a refresh token is valid.                                                                                                                                                       |
| `EX_OAuthServer__RefreshTokenReuseGracePeriodMinutes`         | `60`    | How long a client can reuse a refresh token after it has been exchanged, for clients that retry. `0` turns reuse off.                                                                    |
| `EX_OAuthServer__EnableClientIdMetadataDocuments`             | `true`  | Lets clients use an HTTPS URL as their client ID. Exceptionless downloads the client's metadata document from that URL.                                                                  |
| `EX_OAuthServer__DynamicClientRegistrationIpLimit`            | `20`    | Dynamic client registrations allowed from one IP address per hour.                                                                                                                       |
| `EX_OAuthServer__ClientMetadataDocumentCacheLifetimeMinutes`  | `60`    | How long a downloaded client metadata document is cached.                                                                                                                                |
| `EX_OAuthServer__ClientMetadataDocumentRequestTimeoutSeconds` | `5`     | The timeout for downloading a client metadata document.                                                                                                                                  |
| `EX_OAuthServer__ClientMetadataDocumentMaxBytes`              | `32768` | The largest client metadata document accepted.                                                                                                                                           |
| `EX_Mcp__AllowedOrigins__0`                                   | None    | Extra browser origins allowed to call `/mcp`, one per numbered key. The origins of `EX_BaseURL` and `EX_ApiUrl` are always allowed. Requests without an `Origin` header aren't affected. |

## AI

Exie needs an API key for a model provider. See [AI features](/docs/self-hosting/ai/).

| Environment variable     | Default                                          | What it does                                                                                  |
| ------------------------ | ------------------------------------------------ | --------------------------------------------------------------------------------------------- |
| `EX_Assistant__ApiKey`   | None                                             | The model provider's API key. Exie is unavailable without it.                                 |
| `EX_Assistant__Enabled`  | `true` when an API key is set, otherwise `false` | Turns Exie on or off. A global administrator can override it under **System** > **Settings**. |
| `EX_Assistant__Endpoint` | `https://openrouter.ai/api/v1/chat/completions`  | The chat completions endpoint.                                                                |
| `EX_Assistant__Model`    | `openai/gpt-6.1-sol`                             | The model. A global administrator can override it under **System** > **Settings**.            |

## Reverse proxies

| Environment variable                    | Default | What it does                                                                  |
| --------------------------------------- | ------- | ----------------------------------------------------------------------------- |
| `EX_ForwardedHeaders__Enabled`          | `true`  | Reads `X-Forwarded-For` and `X-Forwarded-Proto` from trusted proxies.         |
| `EX_ForwardedHeaders__ForwardLimit`     | `1`     | How many proxy hops to process.                                               |
| `EX_ForwardedHeaders__KnownProxies__0`  | None    | A trusted proxy IP address, one per numbered key. Loopback is always trusted. |
| `EX_ForwardedHeaders__KnownNetworks__0` | None    | A trusted proxy network in CIDR form, one per numbered key.                   |

See [Reverse proxies and HTTPS](/docs/self-hosting/reverse-proxy/) for how to set these.

## Source maps

Settings for [source maps](/docs/clients/javascript/source-maps/) are in the `SourceMaps` section. The most useful:

| Environment variable                          | Default             | What it does                                                                |
| --------------------------------------------- | ------------------- | --------------------------------------------------------------------------- |
| `EX_SourceMaps__EnableAutoDownload`           | `true`              | Downloads source maps for scripts served over HTTPS when none was uploaded. |
| `EX_SourceMaps__MaximumSourceMapSize`         | `20971520` (20 MB)  | The largest source map, in bytes.                                           |
| `EX_SourceMaps__MaximumGeneratedFileSize`     | `5242880` (5 MB)    | The largest generated JavaScript file, in bytes.                            |
| `EX_SourceMaps__MaximumArtifactsPerProject`   | `1000`              | Source maps kept per project.                                               |
| `EX_SourceMaps__MaximumStorageSizePerProject` | `1073741824` (1 GB) | Source map storage per project, in bytes.                                   |
| `EX_SourceMaps__ArtifactRetentionDays`        | `90`                | Days to keep source maps.                                                   |
| `EX_SourceMaps__RequestTimeoutMilliseconds`   | `3000`              | The timeout for each download.                                              |

The rest of the limits, including separate limits for the Free plan, are in
[SourceMapOptions.cs](https://github.com/exceptionless/Exceptionless/blob/main/src/Exceptionless.Core/Configuration/SourceMapOptions.cs).

## HTTPS, logging, and telemetry

| Environment variable                          | Default                                                | What it does                                                                                             |
| --------------------------------------------- | ------------------------------------------------------ | -------------------------------------------------------------------------------------------------------- |
| `ASPNETCORE_URLS`                             | `http://+:8080` in the images                          | The addresses the app listens on, such as `http://+:8080;https://+:8443`.                                |
| `EX_Kestrel__Certificates__Default__Path`     | None                                                   | A PFX certificate file for HTTPS. Use the `EX_` prefix; `ASPNETCORE_Kestrel__...` variables aren't read. |
| `EX_Kestrel__Certificates__Default__Password` | None                                                   | The certificate's password.                                                                              |
| `EX_Serilog__MinimumLevel__Default`           | `Information`; `Warning` in `Production` and `Staging` | The log level: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, or `Fatal`.                        |
| `OTEL_EXPORTER_OTLP_ENDPOINT`                 | None                                                   | An OpenTelemetry collector. When set, Exceptionless exports traces, metrics, and logs to it.             |

## Hosted service settings

These settings run billing and support on the hosted service. Leave them unset on your own instance. Without
`EX_StripeApiKey`, billing is off and new organizations get the Unlimited plan.

- `EX_StripeApiKey`, `EX_StripePublishableApiKey`, and `EX_StripeWebHookSigningSecret`
- `IntercomId` and `IntercomSecret` in `EX_ConnectionStrings__OAuth`
