---
title: "Kubernetes"
metaTitle: "Self-hosting Exceptionless on Kubernetes with Helm"
description: "Deploy self-hosted Exceptionless on Kubernetes with the Helm chart: values, connection strings, ingress, proxy trust, and migrations."
sources:
  - k8s/exceptionless/Chart.yaml
  - k8s/exceptionless/values.yaml
  - k8s/exceptionless/templates/config.yaml
  - k8s/exceptionless/templates/app.yaml
  - k8s/exceptionless/templates/api.yaml
  - k8s/exceptionless/templates/jobs.yaml
  - k8s/exceptionless/templates/storage.yaml
  - k8s/exceptionless/templates/elasticsearch.yaml
  - k8s/exceptionless/templates/redis.yaml
  - k8s/exceptionless/templates/_helpers.tpl
  - src/Exceptionless.Core/Authentication/ActiveDirectoryLoginProvider.cs
  - src/Exceptionless.Web/Api/Endpoints/StatusEndpoints.cs
---

# Kubernetes

The Exceptionless repository includes a Helm chart that runs the app, the API, and each background job as separate
deployments. This page shows how to install it, which values to set, and how to route traffic to it.

## Requirements

- A Kubernetes cluster and [Helm](https://helm.sh/).
- Elasticsearch 8 with the `mapper-size` plugin, such as the `exceptionless/elasticsearch:8.19.22` image. See
  [Requirements](/docs/self-hosting/#requirements).
- Redis.
- File storage that every pod can share, such as Amazon S3 or Azure Blob Storage.
- An ingress controller or gateway that terminates HTTPS.

## What the chart deploys

| Resource                       | What it runs                                                                                                                   |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------------------------ |
| `<release>-app`                | The `exceptionless/app` image: the web UI and API for your app domains, with real-time updates. Jobs don't run in it.          |
| `<release>-api`                | The `exceptionless/api` image: the API for your API domains, where clients send events. WebSockets are off.                    |
| `<release>-jobs-*` deployments | One deployment per continuous job, such as `event-posts`, `event-notifications`, `mail-message`, `web-hooks`, and `work-item`. |
| `<release>-jobs-*` cron jobs   | Scheduled jobs: `cleanup-data`, `cleanup-orphaned-data`, `download-geoip-database`, and `maintain-indexes`.                    |
| `<release>-jobs-migration`     | A cron job for migrations. Start it by hand after each install or upgrade. See [Run migrations](#run-migrations).              |
| `<release>-config`             | A config map with the `EX_` settings, shared by every pod.                                                                     |

`<release>` is the Helm release name.

## Install

The chart isn't published to a Helm repository. Install it from a clone of the repository:

1. Clone the repository:

   ```bash
   git clone https://github.com/exceptionless/Exceptionless.git
   ```

2. Create a values file, such as `exceptionless-values.yaml`. Replace the domains and connection strings with yours:

   ```yaml
   version: "9.0.0"

   app:
     defaultDomain: errors.example.com
     domains:
       - errors.example.com

   api:
     defaultDomain: api.errors.example.com
     domains:
       - api.errors.example.com

   elasticsearch:
     connectionString: server=http://elasticsearch.example.svc:9200

   redis:
     connectionString: redis.example.svc:6379,abortConnect=false

   storage:
     connectionString: provider=s3;bucket=exceptionless-files;region=us-east-1

   email:
     connectionString: smtp://user:password@smtp.example.com:587

   config:
     EX_SmtpFrom: "Exceptionless <noreply@example.com>"

   otel:
     enabled: false
   ```

3. Install the chart:

   ```bash
   helm install exceptionless ./Exceptionless/k8s/exceptionless \
     --namespace exceptionless --create-namespace \
     --values exceptionless-values.yaml
   ```

4. [Run the migrations](#run-migrations).

5. [Route traffic to the services](#ingress), then open your app domain and sign up. The first account is a global
   administrator.

## Values

| Value                                                                             | Default             | What it does                                                                                                                                                                                                                                                                                     |
| --------------------------------------------------------------------------------- | ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `version`                                                                         | `latest`            | The image tag for the app, API, and job images. Pin it to a release version.                                                                                                                                                                                                                     |
| `appMode`                                                                         | `Production`        | Sets `EX_AppMode`.                                                                                                                                                                                                                                                                               |
| `appScope`                                                                        | None                | Sets `EX_AppScope`, the prefix for index, queue, and cache names.                                                                                                                                                                                                                                |
| `app.defaultDomain`                                                               | None                | Required. The web app's host name. The chart sets `EX_BaseUrl` to `https://` plus this value.                                                                                                                                                                                                    |
| `app.domains`                                                                     | `[]`                | Every host name for the web app. Each is also allowed as an MCP browser origin.                                                                                                                                                                                                                  |
| `api.defaultDomain`                                                               | None                | The API's host name. The chart sets `EX_ExceptionlessServerUrl` to `https://` plus this value, so the app's client setup instructions show it.                                                                                                                                                   |
| `api.domains`                                                                     | `[]`                | Every host name for the API. Each is also allowed as an MCP browser origin.                                                                                                                                                                                                                      |
| `app.image.repository`                                                            | `exceptionless/app` | The app image.                                                                                                                                                                                                                                                                                   |
| `api.image.repository`                                                            | `exceptionless/api` | The API image.                                                                                                                                                                                                                                                                                   |
| `jobs.image.repository`                                                           | `exceptionless/job` | The job image.                                                                                                                                                                                                                                                                                   |
| `app.replicaCount`, `api.replicaCount`                                            | None                | A fixed number of pods.                                                                                                                                                                                                                                                                          |
| `app.maxReplicaCount`, `api.maxReplicaCount`                                      | None                | Adds a CPU-based autoscaler with this many pods at most. `minReplicaCount` sets the minimum, which defaults to 2.                                                                                                                                                                                |
| `jobs.<name>.replicaCount`                                                        | None                | Pods for one job. Names: `closeInactiveSessions`, `dailySummary`, `eventNotifications`, `eventPosts`, `eventUsage`, `eventUserDescriptions`, `mailMessage`, `stackEventCount`, `webhooks`, and `workitem`. `eventPosts` and `workitem` also accept `minReplicaCount` and `maxReplicaCount`.      |
| `elasticsearch.connectionString`                                                  | None                | The Elasticsearch connection string.                                                                                                                                                                                                                                                             |
| `redis.connectionString`                                                          | None                | The Redis connection string.                                                                                                                                                                                                                                                                     |
| `storage.connectionString`                                                        | None                | The storage connection string. Set it. When empty, the chart creates a 5 GiB `ReadWriteMany` volume claim with the `managed-csi-premium` storage class, which exists only on Azure, and mounts it only in the app, API, and `work-item` pods, so the `event-posts` job can't read queued events. |
| `email.connectionString`                                                          | `smtp://localhost`  | The SMTP connection string.                                                                                                                                                                                                                                                                      |
| `cache.connectionString`, `queue.connectionString`, `messagebus.connectionString` | `provider=redis;`   | The cache, queue, and message bus. The defaults use Redis.                                                                                                                                                                                                                                       |
| `config`                                                                          | `{}`                | Any other `EX_` settings, added to the config map as-is.                                                                                                                                                                                                                                         |
| `forwardedHeaders.knownNetworks`                                                  | `[]`                | Trusted ingress networks. See [Trust your ingress](#trust-your-ingress).                                                                                                                                                                                                                         |
| `ingress.enabled`                                                                 | `false`             | Creates ingress resources for Azure Application Gateway for Containers. Leave it off on other clusters.                                                                                                                                                                                          |
| `service.type`                                                                    | `ClusterIP`         | The type of the chart's services.                                                                                                                                                                                                                                                                |
| `otel.enabled`                                                                    | `true`              | Sends OpenTelemetry data to a collector on port 4317 of each node. Set it to `false` unless you run one.                                                                                                                                                                                         |

Always set `elasticsearch.connectionString` and `redis.connectionString` to services you run. When they're empty, the
chart adds single-pod Elasticsearch and Redis deployments that aren't suitable for real use; that Elasticsearch keeps
its data in an `emptyDir` volume, which is lost when the pod restarts.

The chart also sets `EX_EnableSsl` to `true` and turns off the `GeoPlugin` and `LocationPlugin` event plugins.

## Configuration {#configuration}

Every Exceptionless setting can go in the `config` value, using the environment variable names from the
[configuration reference](/docs/self-hosting/configuration/). The chart writes them into the `<release>-config` config
map, which every pod loads:

```yaml
config:
  EX_EnableAccountCreation: "false"
  EX_Assistant__ApiKey: YOUR_OPENROUTER_API_KEY
  EX_ConnectionStrings__OAuth: GitHubId=YOUR_ID;GitHubSecret=YOUR_SECRET
```

Quote values that YAML would otherwise read as booleans or numbers. A config map isn't encrypted, so keep secrets such
as API keys and passwords in a values file you protect, or manage the config map with your own secrets tooling.

Leave `EX_ApiUrl` unset. The web UI then calls the API on the app domain, where WebSockets are on, and OAuth and the MCP
server use the app domain too. Clients send events to the API domain.

After changing values, run `helm upgrade` with the same values file. The pods restart when the config map changes.

## Ingress {#ingress}

Route each app domain to the `<release>-app` service and each API domain to the `<release>-api` service, both on port
80. This example uses the release name `exceptionless`; set `ingressClassName` and TLS for your controller:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: exceptionless
  namespace: exceptionless
spec:
  ingressClassName: YOUR_INGRESS_CLASS
  tls:
    - hosts:
        - errors.example.com
        - api.errors.example.com
      secretName: exceptionless-tls
  rules:
    - host: errors.example.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: exceptionless-app
                port:
                  number: 80
    - host: api.errors.example.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: exceptionless-api
                port:
                  number: 80
```

Your ingress must also allow WebSockets on the app domain, request bodies up to about 25 MB for source map uploads, and
responses that stream for more than two minutes for Exie. See
[What the proxy needs to do](/docs/self-hosting/reverse-proxy/#what-the-proxy-needs-to-do).

### Trust your ingress {#trust-your-ingress}

Exceptionless reads client IP addresses and the HTTPS scheme only from proxies it trusts. If your ingress controller or
gateway connects to the pods from a dedicated subnet, list it:

```yaml
forwardedHeaders:
  knownNetworks:
    - 192.0.2.0/24
```

The chart turns on forwarded headers with a forward limit of 1 and adds each network as
`EX_ForwardedHeaders__KnownNetworks__<n>`. Replacing the list removes old entries, even with `--reuse-values`. Don't
also set `ForwardedHeaders` keys in `config`; the chart refuses to render when you combine them. For trusted proxy
addresses or more than one proxy hop, leave the list empty and use `config` instead:

```yaml
config:
  EX_ForwardedHeaders__KnownProxies__0: "192.0.2.10"
  EX_ForwardedHeaders__ForwardLimit: "1"
```

Trust only addresses your ingress connects from, not the whole cluster network. Use the echo check in
[Reverse proxies and HTTPS](/docs/self-hosting/reverse-proxy/#trust-your-proxy) to find and verify them.

## Run migrations {#run-migrations}

Start the migration job by hand after the first install and after every upgrade:

```bash
kubectl create job --from=cronjob/exceptionless-jobs-migration exceptionless-migration-1 --namespace exceptionless
kubectl wait --for=condition=complete --timeout=20m job/exceptionless-migration-1 --namespace exceptionless
```

Use a new job name each time. Check its progress with
`kubectl logs job/exceptionless-migration-1 --namespace
exceptionless`, or under **Settings** > **System** >
**Migrations** in the app. See [Administration](/docs/self-hosting/administration/#migrations).

## Upgrade {#upgrade}

1. Set `version` to the new release in your values file.
2. Run
   `helm upgrade exceptionless ./Exceptionless/k8s/exceptionless --namespace exceptionless --values exceptionless-values.yaml`,
   from an up-to-date clone of the repository.
3. [Run the migrations](#run-migrations).

See [Upgrading](/docs/self-hosting/upgrading-self-hosted-instance/) for what changed between versions.

## Active Directory authentication {#active-directory-authentication}

Active Directory sign-in uses Windows directory APIs, so it doesn't work in the Linux containers this chart runs. See
[Active Directory](/docs/self-hosting/configuration/#active-directory) for how it works on Windows.

## Troubleshooting

- To check the version you're running, open `/api/v2/about` on your app domain.
- Each pod serves `/health` for liveness and `/ready` for readiness on port 8080.
- For more detailed logs, set `EX_Serilog__MinimumLevel__Default: Debug` in `config` and upgrade.

If you're stuck, open an issue on [GitHub](https://github.com/exceptionless/Exceptionless/issues) or ask on
[Discord](https://discord.gg/xv3sjurVwA).
