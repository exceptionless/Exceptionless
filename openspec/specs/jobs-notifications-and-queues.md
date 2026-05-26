# Spec: Jobs, Notifications & Queues

Baseline spec for background processing, notifications, and queue infrastructure.

> **Implementation-derived values** are included to guide future changes, but they are not public compatibility contracts unless explicitly stated in the relevant requirement.

## Job Runner

- `Exceptionless.Job` runs background workers.
- Default mode (no args): runs all jobs concurrently.
- Named mode (single arg): runs one specific job (e.g., `EventPosts`).
- Aspire launch profile `AllJobs` runs all jobs.

## Jobs

| Job | Queue/Trigger | Purpose |
|-----|---------------|---------|
| EventPostsJob | `IQueue<EventPost>` | Dequeue and process submitted events through pipeline |
| EventUserDescriptionsJob | `IQueue<EventUserDescription>` | Attach user descriptions to events |
| EventNotificationsJob | `IQueue<EventNotification>` | Send event notifications to users |
| WebHooksJob | `IQueue<WebHookNotification>` | Deliver webhook payloads to external URLs |
| MailMessageJob | `IQueue<MailMessage>` | Send email messages via SMTP |
| EventUsageJob | — | Track and enforce plan usage limits |
| StackEventCountJob | — | Update stack occurrence counts |
| StackStatusJob | — | Manage stack status transitions (snooze expiry, etc.) |
| DailySummaryJob | — | Send daily summary email digests |
| CleanupDataJob | — | Remove expired/old data |
| CleanupOrphanedDataJob | — | Remove orphaned records |
| CloseInactiveSessionsJob | — | Close stale sessions |
| DownloadGeoIPDatabaseJob | — | Refresh GeoIP database |
| MaintainIndexes | — | Elasticsearch index lifecycle |
| Migration | — | Schema/data migrations |
| DataMigration | — | Elasticsearch data migration/reindexing (e.g., ES major-version transitions); not run by default in AllJobs |
| WorkItem | `IQueue<WorkItemData>` | Generic work item processing |

## Queues

All queues use Foundatio `IQueue<T>` abstraction:

| Queue Type | Producers | Consumer |
|-----------|-----------|----------|
| `EventPost` | Event submission endpoints | EventPostsJob |
| `EventUserDescription` | User description endpoints | EventUserDescriptionsJob |
| `EventNotification` | Event pipeline | EventNotificationsJob |
| `WebHookNotification` | Event pipeline / stack changes | WebHooksJob |
| `MailMessage` | Various services | MailMessageJob |
| `WorkItemData` | Various services | WorkItem job |

## Queue Infrastructure

- Development: Azure Storage Queue emulator (via Aspire).
- Production: Azure Storage Queues or Redis (via Insulation layer).
- Queue stats visible at `GET /api/v2/queue-stats` (admin).

## Notifications

- **WebSocket push** (`/api/v2/push`): real-time UI updates via `IMessageBus` → `WebSocketConnectionManager`.
- **System notifications**: `GET/POST/DELETE /api/v2/notifications/system` (admin).
- **Release notifications**: `POST /api/v2/notifications/release` (admin).
- **Email**: routed through `MailMessageJob` → SMTP.
- **Webhooks**: configurable per-project, delivered via `WebHooksJob`.
- **Daily summary**: scheduled email digest via `DailySummaryJob`.

## Message Bus

- `IMessageBus` (Foundatio) used for cross-process pub/sub.
- Powers WebSocket push and inter-service coordination.
- Redis-backed in production.

## Internal Implementation Baselines

**Queue retry configuration (implementation-derived, not public contracts):**
- `EventPost` queue: registered with `retries: 1`.
- Most other queues: default retry count of 2.
- `WorkItem` queue: uses a one-hour timeout instead of the default five-minute timeout.

**Job distinction:**
- `Migration` runs in-place schema/data migration jobs (documented for v6→v7 upgrade path).
- `DataMigration` handles Elasticsearch data reindexing for major ES version transitions (documented for v5→v6 and ES major-version upgrades). Kubernetes manifests show these as separate job resources with `args: [DataMigration]` and `args: [Migration]`.

## Requirements

### Requirement: Queue retry behavior is configuration-driven and must be preserved

Exceptionless queue retry behavior is configured per queue type and must not be changed without documenting the operational impact via an OpenSpec change.

#### Scenario: Event post queue is registered

Given the event post queue is registered
When the queue provider is Azure Storage, Redis, or SQS
Then the event post queue uses its configured retry behavior.

#### Scenario: Work item queue is registered

Given the work item queue is registered
When the queue provider is Azure Storage, Redis, or SQS
Then the work item queue uses its configured work-item timeout.

## Compatibility Boundaries

- Queue message schemas (`EventPost`, `EventNotification`, `WebHookNotification`, `MailMessage`, `EventUserDescription`, `WorkItemData`) are internal contracts between API and Job processes.
- WebSocket push message format is a frontend contract.
- Webhook payload format is an external integration contract.
- Job names are used as CLI arguments and in monitoring; renaming is breaking.

### Requirement: Webhook payloads follow API-versioned models

Exceptionless webhook payloads are tied to API-versioned payload models. Webhook documentation references v2 API models. Changes to webhook payload shape must account for API version compatibility.

#### Scenario: Webhook payload shape changes

Given a change modifies webhook event payload fields or structure
When the change is proposed
Then the change must document the affected API model version and compatibility impact.

### Requirement: Queue failure behavior is retry-based unless explicitly changed

Exceptionless queue processing uses configured retry, complete, abandon, and re-enqueue behavior. Changes to this behavior must document operational impact.

#### Scenario: Queue retry behavior changes

Given a change modifies queue retry counts, abandonment behavior, completion behavior, or re-enqueue behavior
When the change is proposed
Then the change must document failure-mode behavior, observability, and operational recovery expectations.

**Dead-letter:** Permanently failed queue items go to the dead-letter queue. No additional explicit dead-letter processing strategy beyond the queue provider's dead-letter mechanism.
