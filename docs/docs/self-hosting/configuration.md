---
title: "Infrastructure Configuration"
---

# Infrastructure Configuration

Exceptionless can infer its cache, message bus, queue, and file storage from technology-named connection strings. Define a technology once and each compatible role will use it automatically.

Configuration sources use the normal application precedence: command-line arguments override `EX_` environment variables, which override ordinary environment variables (including Aspire-injected variables), which override environment-specific and base YAML files. If both `EX_ConnectionStrings__Redis` and `ConnectionStrings__Redis` are present, the `EX_` value wins.

## What changed

This model adds automatic provider selection without changing the public option types. New configurations declare technology connection strings only. Existing Helm, Docker Compose, and self-hosted role selectors remain supported as a compatibility layer. The runtime also keeps Redis connections isolated by their effective connection string, so a legacy role-specific endpoint cannot leak into another role.

![Configuration sources override one another before explicit role selectors or automatic priorities are evaluated.](/assets/img/docs/configuration-precedence.svg)

## Automatic role selection

| Role | Automatic priority |
| --- | --- |
| Cache | `Redis`, then local memory |
| MessageBus | `RabbitMQ`, then `Redis`, then local memory |
| Queue | `AzureQueues`, then `SQS`, then `Redis`, then local memory |
| Storage | `AzureStorage`, then `S3`, then `Aliyun`, then `Folder`, then local memory |

Redis is never selected for file storage. For production file storage, configure durable Azure Blob, S3, Aliyun, or folder storage.

The first configured technology in each role's row wins. Technology connection strings are atomic: extend or replace `ConnectionStrings:Redis` itself rather than defining a second Cache or Queue connection string. Existing `ConnectionStrings:Cache`, `MessageBus`, `Queue`, and `Storage` values still win when present so deployed Helm and Docker configurations remain safe, but they are no longer the recommended configuration model.

![Automatic infrastructure role selection priorities, with Redis intentionally excluded from Storage.](/assets/img/docs/infrastructure-role-selection.svg)

An existing explicit role selector short-circuits this graph. A blank legacy role value is treated as absent.

## Examples

Environment variables use double underscores where YAML or .NET configuration uses colons.

### Redis only

This uses Redis for Cache, MessageBus, and Queue. Storage remains local.

```yaml
EX_ConnectionStrings__Redis: redis:6379,abortConnect=false
```

### Redis with RabbitMQ

RabbitMQ automatically becomes MessageBus while Redis continues to supply Cache and Queue.

```yaml
EX_ConnectionStrings__Redis: redis:6379,abortConnect=false
EX_ConnectionStrings__RabbitMQ: amqps://user:password@rabbitmq:5671/%2F
```

To use Redis for MessageBus instead, omit the `RabbitMQ` technology connection string. Existing deployments may retain `EX_ConnectionStrings__MessageBus=provider=redis` as a compatibility override.

Legacy and inline RabbitMQ forms remain supported:

```yaml
EX_ConnectionStrings__MessageBus: 'provider=rabbitmq;server="amqps://user:password@rabbitmq:5671/%2F"'
# Or: 'provider=rabbitmq;amqps://user:password@rabbitmq:5671/%2F'
```

Percent-encode reserved characters in RabbitMQ usernames, passwords, and virtual hosts.

### Queue and storage technologies

```yaml
# Azure Queue Storage is inferred for Queue.
EX_ConnectionStrings__AzureQueues: DefaultEndpointsProtocol=https;AccountName=example;AccountKey=secret

# Azure Blob Storage is inferred for Storage.
EX_ConnectionStrings__AzureStorage: DefaultEndpointsProtocol=https;AccountName=example;AccountKey=secret

# Folder is a named local storage technology.
EX_ConnectionStrings__Folder: path=/app/storage
```

SQS and S3 use `EX_ConnectionStrings__SQS` and `EX_ConnectionStrings__S3`. Aliyun storage uses `EX_ConnectionStrings__Aliyun`. Do not configure a higher-priority technology for the same role unless that priority is intentional.

Redis and RabbitMQ native connection strings are opaque. Options belong in the complete technology connection string and are never generically concatenated with another role string. Existing `provider=...` selectors and full inline values remain supported for compatibility. New providers must add an allowlisted technology alias, compatible roles, parsing rules, and a fixed priority.

The Redis registration follows the same layering boundary:

![Redis connections are deduplicated only when roles resolve to the same exact connection string; WebSocket mapping always uses the Cache connection.](/assets/img/docs/redis-connection-ownership.svg)

Equal effective strings are deduplicated; different legacy role endpoints remain isolated. Redis telemetry is enabled whenever Redis supplies any role.

### Legacy role controls

```yaml
EX_ConnectionStrings__Cache: local
EX_ConnectionStrings__MessageBus: local
EX_ConnectionStrings__Queue: local
EX_ConnectionStrings__Storage: local
```

These role keys are retained for upgrades and exceptional compatibility needs; new provider-free configurations normally omit them. `local` storage is in memory. Use the named `Folder` technology when local storage must survive restarts.

## Helm, Docker Compose, and Aspire

Existing Helm values and Docker Compose configurations do not need to change. Their explicit `Cache`, `MessageBus`, `Queue`, and `Storage` selectors have the highest role-selection precedence and remain supported. Helm's folder storage setting and persistent-volume behavior are unchanged.

The current Helm chart deliberately operates in legacy-selector mode: it renders explicit values for all four roles. Consequently, adding `EX_ConnectionStrings__RabbitMQ` by itself does **not** switch MessageBus to RabbitMQ because the chart's explicit `MessageBus` value wins. Configure RabbitMQ through the existing Helm value instead:

```yaml
messagebus:
  connectionString: 'provider=rabbitmq;server="amqps://user:password@rabbitmq:5671/%2F"'
```

Do not set any distributed Helm role to `local` when multiple app or job replicas may run. In-memory caches, message buses, queues, and storage are process-local and cannot coordinate replicas.

Aspire injects connection strings as `ConnectionStrings__{resource-name}` environment variables. The existing `Redis`, `AzureStorage`, and `AzureQueues` resource names therefore become `ConnectionStrings__Redis`, `ConnectionStrings__AzureStorage`, and `ConnectionStrings__AzureQueues`, which match the automatic selection aliases. Aspire does not need role selectors. An `EX_ConnectionStrings__{name}` value still wins when both forms are present.

Elasticsearch, email, OAuth, LDAP, and other fixed-service connection strings are not part of infrastructure role selection.

## Rolling out the change

A rolling **version-only** Helm upgrade is compatible with mixed old and new Exceptionless instances when every rendered role selector and every effective connection string remains exactly unchanged. Keep the existing `Cache`, `MessageBus`, `Queue`, and `Storage` values in place while upgrading the images. All overlapping instances then use the same providers and endpoints.

This compatibility statement covers the Exceptionless app and job workloads. A production installation also needs durable, highly available infrastructure; the chart's bundled single-replica Redis and Elasticsearch resources are convenience dependencies, not a zero-downtime production topology.

Removing a selector is safe during normal operation only when the role resolves to the same provider **and the same effective connection string** before and after removal. Compare the resolved result, not merely the technology name. For example, removing `MessageBus=provider=redis` is safe only if automatic selection still chooses Redis with the identical Redis connection string.

Changing a role's provider or endpoint is an infrastructure migration, not a zero-downtime configuration cleanup. During a rolling change, old and new replicas would otherwise be split across message buses, queue backlogs, storage locations, distributed caches, locks, and WebSocket mappings. Use a provider-specific bridge or dual-read/write migration where the technology supports it, or quiesce producers, drain outstanding work, switch every replica together during a maintenance window, and verify the new backend before resuming traffic.

Do not combine a binary upgrade with a selector, provider, or endpoint migration. If a configuration migration must be rolled back, restore the old selector and endpoint first and wait for all replicas to converge before rolling back the application version.

![A safe Helm image rollout keeps selectors and effective endpoints unchanged; provider or endpoint changes follow a separate migration path.](/assets/img/docs/helm-version-rollout.svg)
