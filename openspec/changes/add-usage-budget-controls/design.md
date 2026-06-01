# Design: Add Usage Budget Controls

## Overview

Add three complementary usage budget controls:

1. **Organization budget alert emails** — warnings sent when organization accepted event usage crosses configured percentage thresholds.
2. **Automatic smart project throttling** — automatic project-isolated throttling/sampling when a project spikes enough to threaten organization usage.
3. **Optional project event budgets** — user-configured project-level accepted event caps.

These are event-usage controls, not generic API request throttling controls.

The implementation should extend existing usage and notification paths:

- `UsageService` remains responsible for usage totals, budget threshold detection, smart throughput calculations, and limit calculations.
- Existing organization overage notification behavior remains unchanged.
- `OverageMiddleware` remains the event-post gate for coarse request-level enforcement.
- `EventPostsJob` / event post processing is where project-level sampled acceptance should happen after events are parsed.
- Existing organization usage enforcement remains the outer hard limit.
- Existing `ThrottlingMiddleware` remains unchanged for this feature.
- Budget alert and smart throttling emails are asynchronous side effects and must not block event ingestion.

## Existing code paths

### Event post enforcement

`OverageMiddleware` currently:

1. Skips non-event posts.
2. Gets the default organization id from the authenticated request.
3. Rejects when event submission is globally disabled.
4. Rejects invalid/oversized posts.
5. Calls `UsageService.GetEventsLeftAsync(organizationId)`.
6. Rejects organization overage.
7. Allows the request to continue.

This change keeps that coarse gate but does not rely on it as the only project-level enforcement layer.

### Usage tracking

`UsageService.IncrementTotalAsync(organizationId, projectId, eventCount)` already increments both:

- organization bucket total
- project bucket total

Existing cache key helpers accept an optional `projectId`.

Use these existing counters for organization budget alerts, smart project throttling, and project event budgets. Do not introduce a parallel accepted-event counter.

### Event post processing

`EventPostsJob` already:

- loads project and organization
- parses the post into events
- calculates how many events can be processed
- processes only allowed events
- increments blocked usage for events over plan limit
- increments accepted total usage for processed events
- increments discarded usage for discarded events

Extend this path for sampled project-level acceptance.

### Existing organization notice email path

Existing flow for monthly/hourly overage notices:

1. `UsageService` publishes `PlanOverage`.
2. `EnqueueOrganizationNotificationOnPlanOverage` subscribes to `PlanOverage`.
3. It enqueues `OrganizationNotificationWorkItem`.
4. `OrganizationNotificationWorkItemHandler` loads organization and users.
5. The handler sends organization emails only to users with verified email addresses and email notifications enabled.
6. `Mailer.SendOrganizationNoticeAsync` renders the organization notice email.

Budget alerts and project smart throttling notifications should follow this pattern with new message/work item/mailer/template types.

---

## Automatic Smart Project Throttling

### Overview

Automatic smart project throttling is the default guardrail for sudden event spikes.

It addresses historical feedback that:

- organization-level throttling can punish all projects when one project is noisy
- throttling should notify users when applied
- throttling should still allow a small percentage of events through for troubleshooting
- users should not need to configure many options before this protection works

This is separate from optional project event budgets:

- Smart throttling is automatic and adaptive.
- Project event budgets are explicit user-configured caps.

### Design principle: minimal configuration

Smart project throttling should not require users to configure project percentages, per-project caps, or per-stack settings.

The UI may expose status and explanation, but not a large set of tuning options.

### Throughput calculation

Smart throttling should use remaining monthly allowance and remaining time in the monthly usage period.

Preferred formula shape:

```text
maxThroughput(window) = eventsLeftInMonth / windowsLeftInMonth * burstMultiplier
```

Where:

* `eventsLeftInMonth` is the organization's current effective monthly allowance minus accepted usage.
* `windowsLeftInMonth` is the number of smart-throttling windows remaining in the period.
* `burstMultiplier` preserves existing burst tolerance behavior. A default value such as 10 is acceptable if it matches existing Exceptionless throttle behavior.
* The evaluation window should align with existing usage bucket behavior where possible.

This is intentionally different from a static `monthlyPlanLimit / timeInMonth` calculation. Customers should not be throttled harshly late in the month when they still have substantial allowance remaining.

### Project isolation

When organization-level smart throttling would apply, the system should identify the project or projects contributing to the spike and throttle those projects first.

Observable behavior:

* One noisy project should not cause all other projects in the organization to stop ingesting events.
* Other projects should continue under organization-level usage enforcement unless they also exceed smart-throttling criteria.
* If the organization hard monthly limit is exhausted, all projects remain subject to existing organization overage behavior.

### Sampling while throttled

When a project is smart-throttled and the organization still has remaining monthly allowance, Exceptionless should accept a small sample of events from that project.

Recommended v1 behavior:

```text
sampleRate = 1% to 5%
```

Implementation may use a fixed default such as 1% or 5% for v1, as long as behavior is deterministic/testable and does not expose many configuration options.

Sampling goals:

* Preserve visibility into whether the problem is still occurring.
* Preserve visibility into whether a deployed fix reduced occurrences.
* Avoid accepting 0 events from a throttled project while the organization still has monthly allowance.
* Avoid allowing a noisy project to consume the entire organization allowance.

### Event processing location

Smart throttling and sampled acceptance should be implemented in the event processing path after event posts are parsed, not solely in `OverageMiddleware`.

Reason:

* `OverageMiddleware` can reject the entire request before the event count is known.
* It cannot preserve a 1–5% sample of events from a batch.
* `EventPostsJob` already parses the post, calculates `eventsToProcess`, processes only allowed events, and increments blocked usage for the rest.
* Extending this job/path allows project-level partial acceptance while preserving existing usage accounting.

### Event selection

When only part of a batch can be accepted, the implementation should avoid always taking only the first events if doing so would bias troubleshooting data.

Acceptable v1 approaches:

* deterministic sampling by event id/hash
* random sampling with stable seed per post
* first-N selection only if no safe sampling utility exists, but this should be documented as a known limitation

### Smart throttling state

The system may store project throttling state in cache to avoid recalculating expensive decisions on every event.

Suggested key shape:

```text
usage-smart-throttle:{yyyyMM}:{organizationId}:{projectId}
```

Suggested value:

```json
{
  "is_throttled": true,
  "sample_rate": 0.01,
  "effective_until_utc": "..."
}
```

TTL: short window, aligned with usage bucket/window duration.

If cached throttle state expires, the system can re-evaluate based on current usage counters.

### Smart throttling notification

When a project enters smart-throttled state, eligible organization users should receive an email notification.

Notification behavior:

* Send at most once per project per throttling period or cooldown window.
* Reuse the existing organization notification eligibility rules.
* Do not send repeated emails for every event post while the project remains throttled.
* Include project name, organization name, current accepted usage, current limit context, and a link to project usage.

Suggested message/work item:

```csharp
public record ProjectSmartThrottleApplied
{
    public required string OrganizationId { get; init; }
    public required string ProjectId { get; init; }
    public required double SampleRate { get; init; }
    public required int CurrentEventCount { get; init; }
    public required int EventLimit { get; init; }
}
```

Suggested mailer method:

```csharp
Task SendProjectThrottledNoticeAsync(
    User user,
    Organization organization,
    Project project,
    double sampleRate,
    int currentEventCount,
    int eventLimit);
```

### Relationship to organization overage

Smart project throttling is not a substitute for organization hard overage enforcement.

Behavior:

* If organization monthly allowance remains, smart-throttled projects may still have sampled events accepted.
* If organization monthly allowance is exhausted, existing organization overage behavior applies.
* Project sampling must not allow accepted event totals to exceed the organization hard monthly allowance.

### Relationship to optional project event budgets

If a project has an explicit project event budget, that budget participates in the same allowed-event calculation.

Recommended order:

```text
organization hard allowance
↓
explicit project budget, if configured
↓
automatic smart throttling sample allowance, if active
```

The final number of events accepted from a batch should be the minimum allowed by all applicable controls.

### Relationship to budget alert emails

Smart throttling email notification is distinct from organization budget alert emails.

* Budget alerts notify when organization accepted usage crosses configured percentages.
* Smart throttling notifications notify when the system starts sampling/throttling a noisy project.
* A project can be smart-throttled before any budget alert threshold is crossed.
* A budget alert can fire without any project being smart-throttled.

---

## Organization Budget Alert Emails

### Overview

Add configurable organization-level event budget alert emails. These alerts warn users before monthly plan overage, based on accepted event usage crossing configured percentage thresholds.

Budget alerts are informational only. They do not block ingestion and do not replace organization overage enforcement, smart throttling, or project event budgets.

### Domain model

Add budget alert settings to Organization.

```csharp
public class Organization
{
    // existing properties...
    public OrganizationBudgetAlertSettings? BudgetAlertSettings { get; set; }
}
```

Add a new model:

```csharp
namespace Exceptionless.Core.Models;

public class OrganizationBudgetAlertSettings
{
    public bool Enabled { get; set; }

    /// <summary>
    /// Percentage thresholds of the organization's effective monthly event allowance.
    /// Example: [50, 80, 90].
    /// </summary>
    public SortedSet<int> Thresholds { get; set; } = [];
}
```

### Defaults

Existing organizations:

```text
budget_alert_settings = null
```

Null means disabled.

New organizations:

```text
budget_alert_settings = null
```

The UI may suggest default thresholds [50, 80], but alerts must not be enabled until a user explicitly saves settings.

### Validation

Validation rules:

* `BudgetAlertSettings == null` is valid.
* `Enabled == false` with empty thresholds is valid.
* If enabled, thresholds must contain at least one value.
* Each threshold must be greater than 0.
* Each threshold must be less than 100.
* Threshold 100 is not allowed because existing monthly overage notification already handles reaching/exceeding the plan limit.
* Duplicate thresholds must be removed.
* Thresholds must be stored sorted ascending.
* Percentage budget alerts must be rejected or disabled for unlimited organizations.

### API contract

Add `BudgetAlertSettings` to organization update and view DTOs.

Preferred cleanup:

```csharp
public record UpdateOrganization
{
    public string Name { get; set; } = null!;
    public OrganizationBudgetAlertSettings? BudgetAlertSettings { get; set; }
}
```

Then update `OrganizationController` generic usage from:

```csharp
RepositoryApiController<IOrganizationRepository, Organization, ViewOrganization, NewOrganization, NewOrganization>
```

to:

```csharp
RepositoryApiController<IOrganizationRepository, Organization, ViewOrganization, NewOrganization, UpdateOrganization>
```

If minimizing backend churn is preferred, `BudgetAlertSettings` can be added to `NewOrganization` because the current controller uses `NewOrganization` for both create and update. However, adding `UpdateOrganization` is cleaner because `NewOrganization` currently only contains the required organization name.

Add to `ViewOrganization`:

```csharp
public OrganizationBudgetAlertSettings? BudgetAlertSettings { get; set; }
```

Serialized JSON uses the existing snake_case policy.

### Threshold calculation

Use the same effective organization allowance as existing usage enforcement:

```csharp
effectiveOrganizationAllowance = organization.GetMaxEventsPerMonthWithBonus(timeProvider)
```

If effective allowance is negative/unlimited:

```text
budget alert thresholds are inactive
```

Threshold event count:

```csharp
thresholdEventCount = ceil(effectiveOrganizationAllowance * thresholdPercent / 100)
```

### Triggering alerts

Budget alert checks should run in the accepted-event usage increment path.

Recommended location:

```text
UsageService.IncrementTotalAsync
```

Observable behavior must be:

* Alert fires when usage crosses threshold.
* Alert does not fire before threshold.
* Alert does not fire repeatedly after threshold.
* Multiple crossed thresholds can fire if a large batch jumps over more than one threshold.

### Deduplication

Each organization threshold must email at most once per monthly usage period.

Recommended cache key:

```text
usage-budget-alert:{yyyyMM}:{organizationId}:{threshold}
```

TTL: until end of current monthly usage period + safety buffer.

The cache key should be set before or atomically with enqueueing the work item to reduce duplicate sends under concurrency.

### Message and work item

Add a new message:

```csharp
namespace Exceptionless.Core.Messaging.Models;

public record OrganizationBudgetAlert
{
    public required string OrganizationId { get; init; }
    public required int Threshold { get; init; }
    public required int ThresholdEventCount { get; init; }
    public required int CurrentEventCount { get; init; }
    public required int EventLimit { get; init; }
}
```

Add a new work item:

```csharp
namespace Exceptionless.Core.Models.WorkItems;

public record OrganizationBudgetAlertWorkItem
{
    public required string OrganizationId { get; init; }
    public required int Threshold { get; init; }
    public required int ThresholdEventCount { get; init; }
    public required int CurrentEventCount { get; init; }
    public required int EventLimit { get; init; }
}
```

Add a startup subscriber:

```text
EnqueueOrganizationBudgetAlertOnUsageThreshold
```

### Work item handler

Add:

```text
OrganizationBudgetAlertWorkItemHandler
```

Behavior:

* Load organization by id.
* Load users in organization.
* Send only to users with verified email addresses.
* Send only to users with `EmailNotificationsEnabled == true`.
* Use the same eligibility rules as existing organization notices.
* Do not send if organization no longer exists.
* Re-check current organization budget alert settings before sending.
* If budget alerts are disabled or the threshold was removed, skip sending.

### Mailer

Add to `IMailer`:

```csharp
Task SendOrganizationBudgetAlertAsync(
    User user,
    Organization organization,
    int threshold,
    int thresholdEventCount,
    int currentEventCount,
    int eventLimit);
```

Add implementation in `Mailer`.

Add template:

```text
src/Exceptionless.Core/Mail/Templates/organization-budget-alert.html
```

Template must include:

* organization name
* threshold percentage
* threshold event count
* current accepted event count
* organization event limit
* remaining event count
* link to organization usage
* link to billing/plan management when applicable
* link to notification settings

Do not include event payload data.

---

## Optional Project Event Budgets

### Overview

Add optional project-level event budgets enforced during event processing. This is an event-usage control, not generic API request throttling.

### Domain model

Add a nullable event budget configuration to Project.

```csharp
public class Project
{
    // existing properties...
    public ProjectIngestLimit? IngestLimit { get; set; }
}
```

Add a new model in `src/Exceptionless.Core/Models/ProjectIngestLimit.cs`:

```csharp
namespace Exceptionless.Core.Models;

public class ProjectIngestLimit
{
    public ProjectIngestLimitType Type { get; set; }
    public int? FixedLimit { get; set; }
    public decimal? PercentOfOrganizationLimit { get; set; }
}

public enum ProjectIngestLimitType
{
    Fixed = 0,
    PercentOfOrganizationLimit = 1
}
```

### Naming

Use `IngestLimit`, not `RateLimit`, because the feature limits accepted event volume for a project, not generic request rate.

Use `ProjectIngestLimit`, not `ProjectQuota`, because this is a cap, not a reservation.

### API contract

Add `IngestLimit` to:

```text
src/Exceptionless.Web/Models/Project/UpdateProject.cs
src/Exceptionless.Web/Models/Project/ViewProject.cs
```

Recommended DTO shape:

```csharp
public record UpdateProject
{
    public string Name { get; set; } = null!;
    public bool DeleteBotDataEnabled { get; set; }
    public ProjectIngestLimit? IngestLimit { get; set; }
}

public class ViewProject
{
    // existing properties...
    public ProjectIngestLimit? IngestLimit { get; set; }
    public int? EffectiveIngestLimit { get; set; }
    public bool IsSmartThrottled { get; set; }
    public double? SmartThrottleSampleRate { get; set; }
}
```

### Project update behavior

Use existing `PATCH /api/v2/projects/{id}`. Do not add a new endpoint.

Clear project budget:

```json
{
  "ingest_limit": null
}
```

Fixed budget:

```json
{
  "ingest_limit": {
    "type": "fixed",
    "fixed_limit": 20000
  }
}
```

Percentage budget:

```json
{
  "ingest_limit": {
    "type": "percent_of_organization_limit",
    "percent_of_organization_limit": 20
  }
}
```

### Validation

General validation:

* `ingest_limit == null` is valid and means Off.
* Unknown limit type is invalid.
* Limit values must not be negative.
* Ingest limit validation must not change existing project name validation.

For Fixed:

* `fixed_limit` is required.
* `fixed_limit` must be greater than 0.
* `percent_of_organization_limit` should be ignored or cleared server-side.
* Backend should not reject a fixed limit greater than the current organization limit. The effective cap is clamped during evaluation.

For PercentOfOrganizationLimit:

* `percent_of_organization_limit` is required.
* Percentage must be greater than 0.
* Percentage must be less than or equal to 100.
* `fixed_limit` should be ignored or cleared server-side.
* If the organization currently has an unlimited event allowance, the API should reject setting a percentage cap because a percentage of unlimited is not meaningful.
* If an existing percentage cap later encounters an unlimited organization due to a plan change, enforcement should treat the percentage cap as inactive until the organization returns to a finite allowance.

### Effective project limit calculation

Use the same effective organization max that current usage enforcement uses.

```csharp
private static int? GetEffectiveProjectIngestLimit(ProjectIngestLimit? limit, int organizationMaxEvents)
{
    if (limit is null)
        return null;

    return limit.Type switch
    {
        ProjectIngestLimitType.Fixed when limit.FixedLimit is > 0 =>
            organizationMaxEvents < 0
                ? limit.FixedLimit.Value
                : Math.Min(limit.FixedLimit.Value, organizationMaxEvents),
        ProjectIngestLimitType.PercentOfOrganizationLimit
            when organizationMaxEvents > 0 && limit.PercentOfOrganizationLimit is > 0 =>
                Math.Max(1, (int)Math.Ceiling(organizationMaxEvents * (limit.PercentOfOrganizationLimit.Value / 100m))),
        _ => null
    };
}
```

### UsageService API

Add project-aware/budget-aware event allowance calculation.

The final event count accepted from a parsed batch should be the minimum allowed by:

1. organization hard allowance
2. explicit project event budget, if configured
3. automatic smart throttling sampled allowance, if active

Avoid a design that returns only a Boolean. The event processing path needs a count.

Suggested result:

```csharp
public record EventIngestAllowanceResult
{
    public int EventsAllowed { get; init; }
    public int OrganizationEventsLeft { get; init; }
    public int? ProjectEventsLeft { get; init; }
    public int? EffectiveProjectLimit { get; init; }
    public bool IsSmartThrottled { get; init; }
    public double? SmartThrottleSampleRate { get; init; }
    public string? Reason { get; init; }
}
```

Suggested method:

```csharp
public Task<EventIngestAllowanceResult> GetEventIngestAllowanceAsync(
    string organizationId,
    string projectId,
    int submittedEventCount);
```

Behavior:

* Compute organization events left using existing organization usage logic.
* If organization events left is <= 0, allow 0 events.
* Compute explicit project budget events left if configured.
* Compute smart throttling state/sample allowance if project is noisy.
* Return the minimum allowed count.
* Never return an allowed count greater than submitted event count.
* Never return an allowed count greater than organization events left.

### Refactor current events-left logic

The current organization-only method should be refactored into a shared helper:

```csharp
private async Task<int> GetEventsLeftAsync(
    string organizationId,
    string? projectId,
    int maxEventsPerMonth)
```

Use existing total and bucket cache keys with optional project id.

### OverageMiddleware behavior

`OverageMiddleware` should continue handling request-level event-post gates:

* non-event posts are skipped
* missing organization context is rejected
* globally disabled event submission is rejected
* missing content length is rejected
* oversized posts are rejected
* organization hard overage can still be rejected early when there are no events left

Project-level smart throttling and sampled project-budget behavior should not be implemented solely as early middleware rejection because early rejection drops the entire post and cannot preserve a sample.

If `OverageMiddleware` detects that the organization has no remaining monthly allowance, it may preserve the existing organization overage rejection behavior.

If the organization has remaining allowance, project-level controls should be evaluated later in `EventPostsJob` / `UsageService` after events are parsed.

### Status code behavior

* Organization monthly/bucket hard overage remains `402 PaymentRequired` where existing behavior already returns that status.
* Oversized submissions continue to return `413 RequestEntityTooLarge`.
* Missing content length continues to return `411 LengthRequired`.
* Missing organization context continues to return `401 Unauthorized`.
* Disabled event submission continues to return `503 ServiceUnavailable`.
* Project smart throttling should not require changing the HTTP response for accepted queued posts; the job may accept a sample and block/discard the rest asynchronously.
* Project hard rejection may return `429 TooManyRequests` only in code paths where the server can make an immediate project-level decision without sacrificing sampled acceptance. For the normal queued event-post path, project throttling should be represented by partial processing and blocked/discarded usage accounting rather than a full-request 429.

### Headers

Do not repurpose existing generic API throttle headers for project monthly ingest caps or smart project throttling in this change.

### API throttling middleware

Do not change `ThrottlingMiddleware` for this feature.

### Storage and indexing

Persisted data:

* Existing organizations have `BudgetAlertSettings = null`.
* Existing projects have `IngestLimit = null`.
* No migration/backfill is required.
* Null/default behavior must preserve current behavior.

Elasticsearch:

* Do not add Elasticsearch mappings for `Organization.BudgetAlertSettings` or `Project.IngestLimit` unless filtering/sorting/searching by those fields is required.
* Avoid reindexing.

### Organization Usage UI

Add budget alert settings to:

```text
src/Exceptionless.Web/ClientApp/src/routes/(app)/organization/[organizationId]/usage/+page.svelte
```

Place above the usage chart.

Suggested card:

```text
Budget Alerts
Receive an email when your organization reaches selected percentages of its monthly event allowance.
[ ] Enable budget alerts
Thresholds
[50] [80] [90]
[+ Add threshold]
Current plan allowance: 100,000 events
50% = 50,000 events
80% = 80,000 events
Alerts are sent once per threshold per monthly usage period to organization users who have email notifications enabled.
```

### Project Usage UI

Add project event budget and smart throttling status to:

```text
src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/usage/+page.svelte
```

Copy:

```text
Project Event Budget
Protect your organization's monthly event allowance by limiting how many accepted events this project can use.
This is a cap, not a reservation. Other projects are not guaranteed unused capacity.
```

Smart throttling status copy when active:

```text
Smart throttling is currently active for this project. Exceptionless is accepting a small sample of events so you can continue troubleshooting while protecting your organization's monthly event allowance.
```

Do not expose many tuning knobs.

### Project usage chart behavior

If `effective_ingest_limit` is not null, display the dashed limit line as the project limit.

If `effective_ingest_limit` is null, keep showing the organization limit.

Show smart throttling status as text or badge if active.

### Accessibility

* Slider controls must have accessible labels.
* Numeric inputs must have associated labels.
* Computed threshold/effective limit text should update in screen-reader-friendly text.
* Do not rely on color only.
* Save success/error must use existing toast/error patterns.

### Security

* Only users authorized to update the organization may update budget alert settings.
* Only users authorized to update the project may update project event budgets.
* Budget alert and smart throttling emails are sent only to verified users with email notifications enabled.
* Project budgets do not grant additional access.
* Users cannot increase organization allowance through project budgets.
* Smart throttling must not allow accepted usage beyond organization hard allowance.

### Privacy

No new personal event data is collected.

Budget alert and smart throttling emails include organization/project names and aggregate usage numbers only. They must not include event payload data, stack data, user-identifying event details, or project-specific event contents.

### Failure modes

**Budget alert email queue failure**

If mail queue enqueue fails, do not block event ingestion.

**Smart throttling email queue failure**

If smart throttling notification enqueue fails, do not block event ingestion.

**Dedupe cache failure**

If dedupe cache fails, prefer avoiding ingestion failure. The implementation may skip alert sending rather than risk duplicate emails or ingestion failure.

**Organization/project settings changed after message publication**

Re-load settings in the work item handler. If the relevant notification is disabled or no longer applicable, skip sending.

**Project lookup fails**

If the project cannot be loaded during project-level evaluation, do not block solely due to project-level settings. Continue organization-level enforcement.

**Invalid persisted settings**

Treat invalid persisted settings as inactive for enforcement/notification. Do not crash event ingestion.
