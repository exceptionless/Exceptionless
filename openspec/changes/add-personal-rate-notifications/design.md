# Design: Add Personal Rate Notifications

## Current Architecture Summary

- `Project` stores `NotificationSettings` as a `Dictionary<string, NotificationSettings>` keyed by user ID or integration key.
- Existing `NotificationSettings` are simple booleans: `ReportNewErrors`, `ReportCriticalErrors`, `ReportEventRegressions`, `ReportNewEvents`, `ReportCriticalEvents`, `SendDailySummary`.
- `QueueNotificationAction` (priority 70) enqueues `EventNotification` for existing per-event notifications based on project notification settings.
- `EventNotificationsJob` loads event/project/user and sends Slack/email with existing throttles.
- `Mailer` queues `MailMessage` and `MailMessageJob` sends it.
- `UsageService` already uses cache-backed 5-minute bucket counters for usage tracking via `ICacheClient`.
- Production insulation (`Exceptionless.Insulation`) replaces in-memory cache/queues with Redis/Azure/SQS.

## Scope

v1 is personal rate notifications only. No organization-level rules, no webhooks, no digests. It also follows the existing premium-only occurrence-notification model instead of introducing a new free notification channel.

## Data Model

### RateNotificationRule

```csharp
public class RateNotificationRule : IOwnedByOrganizationAndProjectWithIdentity
{
    public string Id { get; set; }
    public string OrganizationId { get; set; }
    public string ProjectId { get; set; }
    public string UserId { get; set; }
    public int Version { get; set; }
    public string Name { get; set; }
    public bool IsEnabled { get; set; }
    public RateNotificationSignal Signal { get; set; }
    public RateNotificationSubject Subject { get; set; }
    public string? StackId { get; set; }
    public int Threshold { get; set; }
    public TimeSpan Window { get; set; }
    public TimeSpan Cooldown { get; set; }
    public DateTime? SnoozedUntilUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public bool IsDeleted { get; set; }
}
```

### Enums

```csharp
public enum RateNotificationSignal
{
    AllEvents,
    Errors,
    CriticalErrors,
    NewErrors,
    Regressions
}

public enum RateNotificationSubject
{
    Project,
    Stack
}
```

### Design decisions

- Rules are stored separately from `Project.NotificationSettings`.
- Existing `NotificationSettings` should not be expanded because rate rules are richer and independently mutable.
- No tag/environment/time-of-day/external-recipient fields in v1.
- `SnoozedUntilUtc` is also the rule's resume boundary. Manual unsnooze sets it to the current UTC time instead of clearing it so the evaluator can ignore activity gathered during the muted period.

## Repository

### IRateNotificationRuleRepository

```csharp
public interface IRateNotificationRuleRepository : IRepositoryOwnedByOrganizationAndProject<RateNotificationRule>
{
    Task<FindResults<RateNotificationRule>> GetByProjectIdAndUserIdAsync(string projectId, string userId, CommandOptionsDescriptor<RateNotificationRule>? options = null);
    Task<FindResults<RateNotificationRule>> GetEnabledByProjectIdAsync(string projectId, CommandOptionsDescriptor<RateNotificationRule>? options = null);
    Task<long> CountByProjectIdAndUserIdAsync(string projectId, string userId);
}
```

### RateNotificationRuleRepository

Elasticsearch-backed repository following existing patterns (e.g., `StackRepository`, `WebHookRepository`).

## API

### Routes

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v2/users/{userId}/projects/{projectId}/rate-notifications` | List user's rules for project |
| POST | `/api/v2/users/{userId}/projects/{projectId}/rate-notifications` | Create rule |
| GET | `/api/v2/users/{userId}/projects/{projectId}/rate-notifications/{ruleId}` | Get rule |
| PUT | `/api/v2/users/{userId}/projects/{projectId}/rate-notifications/{ruleId}` | Update rule |
| DELETE | `/api/v2/users/{userId}/projects/{projectId}/rate-notifications/{ruleId}` | Delete rule |
| POST | `/api/v2/users/{userId}/projects/{projectId}/rate-notifications/{ruleId}/snooze` | Snooze rule |
| POST | `/api/v2/users/{userId}/projects/{projectId}/rate-notifications/{ruleId}/unsnooze` | Unsnooze rule |

### Authorization

- Current user can manage their own rules.
- Global admin can manage any user's rules.
- User must have access to the project's organization.
- External recipients are not supported in v1.

### Premium gating

- Rate notifications follow the current occurrence-notification premium model.
- Rules may remain persisted across plan downgrade, but countering, evaluation, delivery, and Svelte create/enable controls MUST treat the feature as unavailable until premium is restored.

### Validation

- `threshold > 0`
- `window` must be one of: 1m, 5m, 10m, 15m, 30m, 1h
- `cooldown` must be at least the window duration
- Recommended default cooldown: 30m
- Project subject must not specify `stack_id`
- Stack subject must specify `stack_id`
- `stack_id` must belong to the same project
- User cannot exceed 20 rules per project
- Rule name must be non-empty and ≤ 100 characters

## Rule Index

### RateNotificationRuleIndex service

Purpose:

- Load enabled rules for a project.
- Cache compiled counter definitions briefly.
- Ensure event pipeline can cheaply determine which counters to increment.
- Invalidate project rule index on create/update/delete/snooze/unsnooze.

**Important:** The event pipeline must not increment every possible counter. It must increment only counters required by enabled rules for that project.

Cache key: `rate:v1:rules:project:{projectId}`

## Counter Architecture

### RateCounterService

Uses 1-minute UTC buckets.

### Cache keys

```
rate:v1:count:{epochMinute}:{counterKey}
rate:v1:active:{epochMinute}
rate:v1:cooldown:{ruleId}:{subjectKey}
rate:v1:rules:project:{projectId}
```

### Counter key examples

```
project:{projectId}:signal:Errors
project:{projectId}:signal:CriticalErrors
project:{projectId}:signal:NewErrors
project:{projectId}:signal:Regressions
project:{projectId}:signal:AllEvents
project:{projectId}:stack:{stackId}:signal:Errors
project:{projectId}:stack:{stackId}:signal:CriticalErrors
project:{projectId}:stack:{stackId}:signal:NewErrors
project:{projectId}:stack:{stackId}:signal:Regressions
project:{projectId}:stack:{stackId}:signal:AllEvents
```

### TTL guidance

- Counter bucket TTL: 3 hours
- Active bucket TTL: 3 hours
- Cooldown TTL: configured cooldown duration

### Signal matching

| Signal | Matches when |
|--------|-------------|
| AllEvents | Any event |
| Errors | `ev.IsError()` |
| CriticalErrors | `ev.IsError() && ev.IsCritical()` |
| NewErrors | `ctx.IsNew && ev.IsError()` |
| Regressions | `ctx.IsRegression` |

## Event Pipeline Action

### UpdateRateCountersAction

- Runs after stack assignment (priority > 70, e.g., 75)
- Exits fast when the organization does not have premium features
- Loads `RateNotificationRuleIndex` for project
- Exits fast if no enabled rules
- Skips events on stacks where `!ctx.Stack.AllowNotifications`
- Skips canceled/discarded events that would not produce occurrence notifications
- Skips requests already marked as bots by request-info enrichment
- Matches event against compiled counter definitions
- Increments matching counters via `RateCounterService`
- Adds counter key to active bucket list/set
- Never sends notifications directly
- Never queries Elasticsearch per event

## Evaluator Job

### RateNotificationEvaluatorJob

- Runs periodically (recommended: every 60 seconds)
- Acquires distributed lock so only one evaluator runs per cluster
- Inspects recently active counters from active bucket sets
- Skips organizations without premium features
- Sums buckets for each rule's configured window
- Uses `max(windowStartUtc, rule.SnoozedUntilUtc)` as the lower bound when a snooze boundary falls inside the evaluation window so a rule resumes from a fresh baseline
- Skips disabled rules
- Skips snoozed rules (where `SnoozedUntilUtc > now`)
- Compares observed count ≥ threshold
- Enforces cooldown per rule + subject
- Enqueues `RateNotification` work item on threshold crossing
- Sets cooldown key when enqueue succeeds
- Logs fired/skipped reasons with structured context

This v1 does NOT need a full Normal/Pending/Firing/Recovering state machine. Simple threshold + cooldown + snooze is sufficient.

### Snooze semantics

- Snooze suppresses delivery immediately.
- When a snooze expires or a user manually unsnoozes a rule, the rule resumes from a fresh baseline.
- Activity observed entirely during the snooze window MUST NOT trigger an immediate post-snooze alert, even when another enabled rule kept the shared counter hot.

## Queue Model

### RateNotification

```csharp
public class RateNotification
{
    public string RuleId { get; set; }
    public int RuleVersion { get; set; }
    public string OrganizationId { get; set; }
    public string ProjectId { get; set; }
    public string UserId { get; set; }
    public string SubjectKey { get; set; }
    public string? StackId { get; set; }
    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
    public long ObservedCount { get; set; }
    public int Threshold { get; set; }
}
```

## Delivery Job

### RateNotificationsJob

- Loads rule by ID
- Loads project
- Loads user
- Loads stack for stack-scoped rules so email copy can include stack title and deep link
- Validates:
  - Rule still exists
  - Rule is enabled
  - Rule version matches or is compatible
  - User belongs to organization
  - User email is verified
  - User email notifications are enabled
  - Project/org still exists
- Sends email through `IMailer`
- Skips with structured logs when validation fails
- Does not send Slack/webhooks in v1 (marked as future work)

## Lifecycle cleanup

- Remove rate notification rules when a user loses organization access.
- Remove rate notification rules when a project or organization is deleted.
- Invalidate cached rule indexes when cleanup runs so orphaned rules stop consuming evaluator work immediately.
- Reuse the same work-item cleanup pattern already used for `Project.NotificationSettings` updates where practical.

## Email

### IMailer.SendRateNotificationAsync

```csharp
Task SendRateNotificationAsync(User user, Project project, RateNotificationRule rule, long observedCount, DateTime windowStart, DateTime windowEnd, Stack? stack);
```

Email includes:

- Rule name
- Project name
- Observed count
- Threshold
- Window
- Subject type (project or stack)
- Stack title (when subject is stack and available)
- Link to project or stack
- Cooldown explanation
- No "everything is fine" messaging

**Example subject:** `[ProjectName] Error rate exceeded`

**Example body:**

```
Rule: Production error storm
Observed: 241 errors in 5 minutes
Threshold: 100 errors in 5 minutes
Cooldown: Further notifications for this rule are suppressed for 30 minutes.
```

## Frontend

### Feature module

`src/Exceptionless.Web/ClientApp/src/lib/features/rate-notifications/`

UI supports:

- List rules for current user/project
- Create rule
- Edit rule
- Delete rule
- Enable/disable rule
- Snooze/unsnooze rule
- Disabled/upgrade state when the organization lacks premium features

### Form fields

- Name
- Signal (dropdown: All Events, Errors, Critical Errors, New Errors, Regressions)
- Subject (Project or Stack)
- Stack selector (shown when Subject = Stack)
- Threshold (number)
- Window (dropdown: 1m, 5m, 10m, 15m, 30m, 1h)
- Cooldown (duration, minimum = window)
- Enabled (toggle)

### Not built in v1

- Rule history tab
- Delivery history
- Action builder
- Webhook builder
- Slack builder
- Digest UI
- Quiet hours UI
- Organization inheritance UI
- Preview charts

### Noise warning

Display when creating/editing: "This rule may be noisy. Use a cooldown to avoid repeated emails."

## Metrics and Logging

### Metrics

- `rate_notification.rules.loaded`
- `rate_notification.counters.incremented`
- `rate_notification.evaluator.runs`
- `rate_notification.evaluator.rules_evaluated`
- `rate_notification.evaluator.notifications_enqueued`
- `rate_notification.delivery.sent`
- `rate_notification.delivery.skipped`

### Structured log fields

- `RuleId`
- `ProjectId`
- `UserId`
- `ObservedCount`
- `Threshold`
- `Reason` (skipped/fired)

## Bootstrap / DI

- Register `IRateNotificationRuleRepository` / `RateNotificationRuleRepository`
- Register `RateNotificationRuleIndex`
- Register `RateCounterService`
- Register `RateNotificationEvaluatorJob`
- Register `IQueue<RateNotification>` notification queue
- Register `RateNotificationsJob` (delivery)
- Update `Exceptionless.Insulation` queue registration for Redis/Azure/SQS providers
- Add queue health check if existing patterns support it

## Testing Strategy

### Unit tests

- Rule validation (threshold, window, cooldown, subject/stack consistency, name)
- Counter key builder
- Counter increments
- Bucket summing
- Signal matching
- Premium gating
- `AllowNotifications` / bot suppression checks
- Cooldown behavior
- Snooze behavior
- Fresh-baseline behavior after snooze expiry or manual unsnooze
- Evaluator threshold crossing logic

### Integration tests

- User can CRUD own rules
- User cannot manage another user's rules
- Global admin can manage another user's rules
- Stack rule rejects stack from another project
- Non-premium organizations do not counter, evaluate, or deliver rate notifications
- Events on stacks with `AllowNotifications = false` do not increment rate counters
- Bot-marked requests do not increment rate counters
- Evaluator enqueues notification when threshold crossed
- Evaluator does not enqueue below threshold
- Evaluator respects cooldown
- Evaluator respects snooze
- Activity gathered during snooze does not fire immediately when the rule resumes
- Delivery skips disabled rule
- Delivery skips unverified email
- Delivery skips user not in org
- Delivery sends email for valid rule
- Delivery loads stack context for stack-scoped emails
- Membership/project/org cleanup removes orphaned rules

### Not tested in v1

- Action execution
- Webhooks
- Slack
- Digests
- No-data alerts
- Backtesting
- Rule history UI
