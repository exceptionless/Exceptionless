---
title: "Self-hosting"
metaTitle: "Self-hosting Exceptionless: overview and requirements"
description: "Run Exceptionless on your own servers: the Docker images, Elasticsearch and Redis requirements, and storage and email options."
sources:
  - Dockerfile
  - build/docker/elasticsearch/8.x/Dockerfile
  - samples/docker-compose.yml
  - samples/docker-compose.all-in-one.yml
  - k8s/exceptionless/values.yaml
  - src/Exceptionless.Core/Configuration/AppOptions.cs
  - src/Exceptionless.Core/Configuration/ElasticsearchOptions.cs
  - src/Exceptionless.Core/Configuration/StorageOptions.cs
  - src/Exceptionless.Core/Configuration/EmailOptions.cs
  - src/Exceptionless.Insulation/Bootstrapper.cs
  - src/Exceptionless.Job/JobRunnerOptions.cs
  - src/Exceptionless.Web/Api/Handlers/AuthHandler.cs
  - src/Exceptionless.Web/Api/Handlers/OrganizationHandler.cs
---

# Self-hosting

Exceptionless is open source, and you can run the same app that powers the hosted service on your own servers. This page
explains the pieces you run and what they need. To get started, follow the [Docker guide](/docs/self-hosting/docker/).

## Ways to run it

- **Docker Compose** is the recommended setup. It runs the app, the background jobs, Elasticsearch, and Redis as
  separate containers. See [Docker](/docs/self-hosting/docker/).
- **The all-in-one image** runs everything, including Elasticsearch, in one container. Use it to try Exceptionless, not
  for production. See [Try it with one container](/docs/self-hosting/docker/#try-it-with-one-container).
- **Kubernetes** with the Helm chart in the repository. See [Kubernetes](/docs/self-hosting/kubernetes/).

## Architecture

Exceptionless is an ASP.NET Core app with a Svelte web UI. Clients post events to the API, which stores the raw
submission in file storage and queues it. A background job processes the queue, groups events into stacks, and writes
them to Elasticsearch. Other jobs send notifications and email, call webhooks, clean up old data, and run migrations.

The images are published to Docker Hub:

| Image                         | What it runs                                                                                                                                          |
| ----------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `exceptionless/app`           | The web UI and the API in one process on port 8080. It runs the background jobs in the same process unless you set `EX_RunJobsInProcess=false`.       |
| `exceptionless/api`           | The API without the web UI, on port 8080.                                                                                                             |
| `exceptionless/job`           | The background jobs. With no arguments it runs every job, including migrations. Pass a job name, such as `EventPosts` or `Migration`, to run one job. |
| `exceptionless/exceptionless` | The all-in-one image: the app, the jobs, and a single-node Elasticsearch in one container, on port 8080.                                              |
| `exceptionless/elasticsearch` | Elasticsearch 8.19.22 with the `mapper-size` plugin installed. Use it, or install the plugin in your own cluster.                                     |

The app and job images run the same code and read the same [configuration](/docs/self-hosting/configuration/), so give
them the same settings.

## Requirements {#requirements}

- **Elasticsearch 8.** The images and samples use Elasticsearch 8.19.22. Outside development mode, Exceptionless uses
  the `mapper-size` plugin. Install it in your cluster, use the `exceptionless/elasticsearch` image, or add
  `enable-size-plugin=false` to the Elasticsearch connection string.
- **Redis**, when the app and the jobs run in separate processes. Redis holds the shared cache, the queues, and the
  message bus. Without it, Exceptionless uses in-memory versions that only work inside one process, like the all-in-one
  image. The sample Compose file uses `redis:7.4-alpine`.
- **File storage** for queued event submissions and other files. Choose one:
  - A folder, shared by the app and job containers (`provider=folder`). The `app` and all-in-one images default to
    `/app/storage`.
  - Amazon S3 (`provider=s3`).
  - Azure Blob Storage (`provider=azurestorage`).
  - Alibaba Cloud OSS (`provider=aliyun`).
- **An SMTP server** to send email: sign-up verification, password resets, invites, notifications, and daily summaries.
  Without one, Exceptionless runs but sends no email.
- **The public URL** of your web app, set as `EX_BaseURL`. Links in email, OAuth sign-in, and the MCP server use it.

Every setting, with its environment variable and default, is in the
[configuration reference](/docs/self-hosting/configuration/).

## Accounts and plans

The first user to sign up on a new instance becomes a global administrator and can open the
[system administration pages](/docs/self-hosting/administration/). To stop strangers from signing up, set
`EX_EnableAccountCreation=false` after you create your account; people can then join only through an organization
invite.

Billing is off unless you configure Stripe, so new organizations get the Unlimited plan: no event, project, or user
limits, and events are kept for `EX_MaximumRetentionDays` (180 days by default).

## Reverse proxies and HTTPS {#reverse-proxies-and-https}

If a reverse proxy or load balancer terminates HTTPS in front of Exceptionless, tell Exceptionless which proxy addresses
to trust. It reads `X-Forwarded-For` and `X-Forwarded-Proto` only from loopback and from the proxies you list, so
without that setting every request appears to come from the proxy. See
[Reverse proxies and HTTPS](/docs/self-hosting/reverse-proxy/).

## AI features

Exie, the in-app assistant, needs an API key for a model provider. The MCP server for coding agents works without one.
See [AI features](/docs/self-hosting/ai/).
