---
title: "Upgrading"
metaTitle: "Upgrade a self-hosted Exceptionless instance"
description: "Upgrade self-hosted Exceptionless to 9.0: run migrations, configure trusted reverse proxies, and review the changes for operators."
sources:
  - src/Exceptionless.Core/Jobs/Elastic/MigrationJob.cs
  - src/Exceptionless.Core/Bootstrapper.cs
  - src/Exceptionless.Job/JobRunnerOptions.cs
  - src/Exceptionless.Core/Migrations/MigrationRerunService.cs
  - src/Exceptionless.Web/Security/TrustedForwardedHeadersOptions.cs
  - src/Exceptionless.Core/Configuration/AppOptions.cs
  - src/Exceptionless.Core/Configuration/AuthOptions.cs
  - src/Exceptionless.Core/Configuration/AssistantOptions.cs
  - src/Exceptionless.Web/Security/FrontendContentSecurityPolicy.cs
  - src/Exceptionless.Web/ClientApp/src/lib/features/navigation/legacy-links.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/auth/index.svelte.ts
  - build/update-config.sh
  - Dockerfile
  - k8s/exceptionless/templates/config.yaml
---

# Upgrading

This page explains how to upgrade a self-hosted instance and what changed in 9.0 for the people who run it. It covers
upgrades from 8.x.

## Before you upgrade

1. Check the version you're running at `/api/v2/about` on your instance.
2. Read [what changed in 9.0](#what-changed-in-9-0) and update your configuration first, especially if a reverse proxy
   sits in front of Exceptionless.
3. Take an Elasticsearch snapshot.

If you run a version older than 8.0, upgrade to 8.x first. Version 8 moved to Elasticsearch 8; see the
[8.0.0 release notes](https://github.com/exceptionless/Exceptionless/releases/tag/v8.0.0).

## Upgrade

**Docker Compose:** pull the new images and recreate the containers:

```bash
docker compose pull
docker compose up -d
```

If you pin image tags, change them to the new version first.

**Kubernetes:** set `version` in your values file, run `helm upgrade`, and then start the migration job. See
[Kubernetes](/docs/self-hosting/kubernetes/#upgrade).

## Migrations

Some versions change how data is stored in Elasticsearch. Exceptionless applies these changes with migrations:

- The `job` image runs pending migrations each time it starts with no arguments or with `Migration`.
- The `app` and all-in-one images run them at startup when jobs run in the same process.
- In Kubernetes, you start the migration job yourself after each upgrade.

To see progress and failures, go to **Settings** > **System** > **Migrations**, or read the job's logs. See
[Administration](/docs/self-hosting/administration/#migrations).

## Rerun a migration {#rerunning-a-completed-migration}

Some completed migrations can be run again. Take a snapshot first, and stop older instances. You can rerun one from
**Settings** > **System** > **Migrations**, or from the job image with `Migration --rerun MIGRATION_NAME`. See
[Rerun a migration](/docs/self-hosting/administration/#rerunning-a-completed-migration).

## What changed in 9.0 {#what-changed-in-9-0}

### Reverse proxies need to be trusted

Exceptionless now reads `X-Forwarded-For` and `X-Forwarded-Proto` only from loopback addresses and from proxies you
list. Earlier versions accepted them from anyone.

If a reverse proxy, load balancer, or ingress controller sits in front of Exceptionless, add its address before you
upgrade. Otherwise every request appears to come from the proxy over HTTP, and all anonymous visitors share one set of
IP-based limits. See [Reverse proxies and HTTPS](/docs/self-hosting/reverse-proxy/). If clients connect directly, you
don't need to change anything.

Exceptionless refuses to start if these settings are invalid, so check the logs after the upgrade.

### The new web UI replaces the old one

The Angular UI is gone. The Svelte UI, which 8.x served at `/next`, is now at the root of the site.

- Remove `EX_Html5Mode`; it no longer does anything.
- Set `EX_BaseURL` to the plain URL of your app, such as `https://errors.example.com`, without `/#!`.
- Old bookmarks that use `#!/` links are redirected to the new pages.

### Sign-in providers

- Microsoft sign-in now uses the Microsoft identity platform: the `common` endpoint and the `User.Read` scope. Check
  that your app registration in Microsoft Entra accepts accounts from any organizational directory and lists your web
  app's origin as a **Web** redirect URI. The keys are still `MicrosoftId` and `MicrosoftSecret`.
- Facebook sign-in is removed, and `FacebookId` and `FacebookSecret` are ignored. People who signed in only with
  Facebook can set a password with **Forgot password?** on the sign-in page.

See [Sign-in providers](/docs/self-hosting/configuration/#sign-in-providers).

### New and changed settings

- `EX_ApiUrl` is now also read by the server. OAuth discovery and the MCP server use it, and fall back to `EX_BaseURL`.
  Set it only if the API is on a different host name than the web app.
- `EX_EnableSsl` now also makes the app send a Content Security Policy that upgrades insecure requests.
- `EX_ForwardedHeaders__*` settings control proxy trust. The Helm chart adds `forwardedHeaders.knownNetworks`.
- Exie's default model is now `openai/gpt-6.1-sol`. If you didn't set `EX_Assistant__Model` or choose a model under
  **Settings** > **System** > **Settings**, Exie switches to it.

### Elasticsearch

9.0 still uses Elasticsearch 8, so you don't need to reindex. The images and samples now use Elasticsearch 8.19.22.
