---
title: "Infrastructure Configuration"
---

# Infrastructure Configuration

Exceptionless can infer its cache, message bus, queue, and file storage from technology-named connection strings. Define a technology once and each compatible role will use it automatically.

Configuration sources use the normal application precedence: command-line arguments override `EX_` environment variables, which override ordinary environment variables (including Aspire-injected variables), which override environment-specific and base YAML files. If both `EX_ConnectionStrings__Redis` and `ConnectionStrings__Redis` are present, the `EX_` value wins.

## What changed

This model adds automatic provider selection without changing the public option types. New configurations declare technology connection strings only. Existing Helm, Docker Compose, and self-hosted role selectors remain supported as a compatibility layer. The runtime also keeps Redis connections isolated by their effective connection string, so a legacy role-specific endpoint cannot leak into another role.

```mermaid
flowchart LR
    yaml["Base and environment YAML"] --> ordinary["Ordinary / Aspire variables"]
    ordinary --> ex["EX_ environment variables"]
    ex --> cli["Command-line arguments"]
    cli --> effective["Effective configuration"]
    effective --> selector{"Legacy role selector present?"}
    selector -->|"Yes"| explicit["Validate and preserve existing behavior"]
    selector -->|"No"| priority["Try the role's fixed provider priority"]
    priority --> fallback["Use local in-memory implementation"]
```

## Automatic role selection

| Role | Automatic priority |
| --- | --- |
| Cache | `Redis`, then local memory |
| MessageBus | `RabbitMQ`, then `Redis`, then local memory |
| Queue | `AzureQueues`, then `SQS`, then `Redis`, then local memory |
| Storage | `AzureStorage`, then `S3`, then `Aliyun`, then `Folder`, then local memory |

Redis is never selected for file storage. For production file storage, configure durable Azure Blob, S3, Aliyun, or folder storage.

The first configured technology in each role's row wins. Technology connection strings are atomic: extend or replace `ConnectionStrings:Redis` itself rather than defining a second Cache or Queue connection string. Existing `ConnectionStrings:Cache`, `MessageBus`, `Queue`, and `Storage` values still win when present so deployed Helm and Docker configurations remain safe, but they are no longer the recommended configuration model.

```mermaid
flowchart LR
    redis["Redis"] --> cache["Cache"]
    redis --> bus["MessageBus"]
    redis --> queue["Queue"]
    rabbit["RabbitMQ"] --> bus
    azureQueue["AzureQueues"] --> queue
    sqs["SQS"] --> queue
    azureStorage["AzureStorage"] --> storage["Storage"]
    s3["S3"] --> storage
    aliyun["Aliyun"] --> storage
    folder["Folder"] --> storage
    redis -. "never" .-> storage
```

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

```mermaid
flowchart TB
    redis["ConnectionStrings:Redis"] --> registry["RedisConnectionRegistry\nexact effective string"]
    registry --> connection["One shared Redis connection"]
    connection --> cacheClient["Cache"]
    connection --> busClient["MessageBus fallback"]
    connection --> queueClient["Queue fallback"]
    connection --> mapping["WebSocket mapping"]
    legacy["Legacy full role endpoint"] -. "compatibility only" .-> registry
```

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

Aspire injects connection strings by resource name. The existing `Redis`, `AzureStorage`, and `AzureQueues` resource names match the automatic selection aliases, so Aspire does not need role selectors.

Elasticsearch, email, OAuth, LDAP, and other fixed-service connection strings are not part of infrastructure role selection.

## Rolling out the change

The safe migration path is additive. Deploy the resolver while keeping existing explicit role selectors; old and new instances then calculate the same providers. After all app and job instances run the new version, remove one selector at a time for the roles you want to infer. Keep a selector for any role that must stay on a particular endpoint or provider.

For example, with Redis and RabbitMQ both configured, remove `MessageBus` only when you want the automatic RabbitMQ choice. Leave `Cache=provider=redis` and `Queue=provider=redis` in place if those roles should continue using Redis. To roll back, restore the selector values; no data migration is required.

```mermaid
flowchart LR
    old["Existing explicit selectors"] --> deploy["Deploy resolver with selectors unchanged"]
    deploy --> verify["Verify effective providers and health"]
    verify --> simplify["Remove one role selector at a time"]
    simplify --> stable["Technology strings own only intended roles"]
    simplify -. "rollback" .-> old
```
