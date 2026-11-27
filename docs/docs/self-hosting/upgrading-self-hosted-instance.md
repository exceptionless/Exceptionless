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
  - k8s/exceptionless/templates/app.yaml
  - k8s/exceptionless/templates/jobs.yaml
  - src/Exceptionless.Core/Migrations/010_AddEventEnvironment.cs
  - src/Exceptionless.Job/Program.cs
  - src/Exceptionless.Job/appsettings.Production.yml
---

# Upgrading

This page explains how to upgrade a self-hosted instance and what changed in 9.0 for the people who run it. It covers
upgrades from 8.x.

## Before you upgrade

1. Check the version you're running at `/api/v2/about` on your instance.
2. Read [what changed in 9.0](#what-changed-in-9-0) and update your configuration first, especially if a reverse proxy
   sits in front of Exceptionless.

If you run a version older than 8.0, upgrade to 8.x first. Version 8 moved to Elasticsearch 8; see the
[8.0.0 release notes](https://github.com/exceptionless/Exceptionless/releases/tag/v8.0.0).

## Upgrade {#upgrade}

Migrations must finish before the new version accepts or processes events. In 9.0, for example, a migration adds the
`environment` field to existing event indexes; events stored in those indexes before it finishes can't be filtered or
counted by environment. Upgrade in this order:

1. Take an Elasticsearch snapshot.
2. Stop everything that accepts or processes events: the app, the API, and the jobs. Clients keep queued events and
   retry later, but a client that exits before then loses events it keeps only in memory.
3. Run the new version's migration job on its own and wait for it to exit.
4. Search its log for `Failed running migration` and `failed to complete`. Either one means a migration failed. The job
   exits normally even when a migration fails, so don't rely on its exit status. In `Production` mode the job logs only
   warnings and errors, so a successful run may print nothing about migrations.
5. Start the new version of the app, API, and jobs.
6. Open **Settings** > **System** > **Migrations** and check that **Failed** is 0, then send a test event and find it in
   the app.

If a migration fails, keep the app and jobs stopped. Fix the cause shown in the log and run the migration job again; a
migration that can't resume says what to do in its `failed to complete` message. If you can't fix it, restore the
snapshot from step 1 and start the previous version. Going back to the old images without the snapshot doesn't undo a
migration.

### Docker Compose {#upgrade-docker-compose}

With the [Docker Compose setup](/docs/self-hosting/docker/), set the new version in the `app` and `jobs` image tags in
`compose.yaml` first. If you use `latest`, `docker compose pull` fetches the new images. Then:

```bash
docker compose stop app jobs
docker compose pull
docker compose run --rm --no-deps -T jobs Migration 2>&1 | tee migration.log
grep -E "Failed running migration|failed to complete" migration.log
docker compose up -d
```

`--no-deps` keeps Compose from starting the `app` container, which the `jobs` service depends on.

### Kubernetes {#upgrade-kubernetes}

With the [Helm chart](/docs/self-hosting/kubernetes/), save this as `exceptionless-stopped.yaml`. It scales every
deployment that accepts or processes events to zero:

```yaml
app:
  replicaCount: 0
api:
  replicaCount: 0
jobs:
  closeInactiveSessions:
    replicaCount: 0
  dailySummary:
    replicaCount: 0
  eventNotifications:
    replicaCount: 0
  eventPosts:
    replicaCount: 0
  eventUsage:
    replicaCount: 0
  eventUserDescriptions:
    replicaCount: 0
  mailMessage:
    replicaCount: 0
  stackEventCount:
    replicaCount: 0
  webhooks:
    replicaCount: 0
  workitem:
    replicaCount: 0
```

Set `version` to the new release in your values file, then run these commands from an up-to-date clone of the
repository. Use a new job name each time:

```bash
helm upgrade exceptionless ./Exceptionless/k8s/exceptionless --namespace exceptionless \
  --values exceptionless-values.yaml --values exceptionless-stopped.yaml --wait
kubectl create job --from=cronjob/exceptionless-jobs-migration exceptionless-migration-2 --namespace exceptionless
kubectl wait --for=condition=complete --timeout=20m job/exceptionless-migration-2 --namespace exceptionless
kubectl logs job/exceptionless-migration-2 --namespace exceptionless \
  | grep -E "Failed running migration|failed to complete"
helm upgrade exceptionless ./Exceptionless/k8s/exceptionless --namespace exceptionless \
  --values exceptionless-values.yaml
```

The first `helm upgrade` also points the migration cron job at the new image, so the job you create runs the new
version's migrations.

### One container

The all-in-one `exceptionless/exceptionless` image, and an `app` container with `EX_RunJobsInProcess` set to `true`, run
migrations in the same process as the jobs that process events, so they can't follow this order. Use them for testing,
or take a snapshot before you upgrade them.

## Migrations

Some versions change how data is stored in Elasticsearch. Exceptionless applies these changes with migrations:

- The `job` image runs pending migrations each time it starts with no arguments or with `Migration`. With no arguments,
  it runs them alongside every other job.
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
