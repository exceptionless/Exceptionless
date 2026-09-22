# Proposal: Add Usage Budget Controls

## Summary

Add usage budget controls that help organizations both **warn before overage** and **prevent noisy projects from consuming the full organization event allowance**.

This change includes three related capabilities:

1. **Organization budget alert emails** — configurable percentage thresholds such as 50% and 80% of the organization's monthly event allowance. When accepted event usage crosses a configured threshold, Exceptionless sends a budget alert email to eligible organization users.
2. **Automatic smart project throttling** — when a project/environment starts sending enough events to threaten the organization's budget, Exceptionless should automatically isolate throttling to the noisy project where possible, instead of stopping ingestion for the entire organization.
3. **Optional project event budgets** — project-level budget caps that allow customers to explicitly limit how many accepted events a project can consume in the monthly usage period.

Budget alerts are warning controls. Smart project throttling is the default automatic guardrail. Project event budgets are optional prevention controls for customers who want explicit per-project limits.

This change addresses customer feedback asking for budget alerts before plan overage:

> Hi support team
>
> Is there any budget alerts we can configure in terms of the number of events? For example I want to receive an email when i have reached 50% of my plan, and then when 80% of my plan.
>
> This month we have exceeded twice our plan, and we should have a mechanism to prevent this situation.

It also addresses historical customer feedback from issue #112 requesting smarter throttling:

- project-level throttling
- email notification when throttling is applied
- 1–5% of errors delivered even under throttling

## User-visible behavior

### Organization budget alerts

Users managing an organization can configure event budget alert thresholds from Organization Settings → Usage.

Supported behavior:

- Alerts are disabled by default for existing organizations.
- Users can enable budget alerts and configure one or more percentage thresholds.
- Example thresholds: 50%, 80%, 90%.
- A threshold represents the organization's current monthly event allowance, including active bonus events if those are included by existing usage enforcement.
- When accepted usage crosses a configured threshold for the first time in a monthly usage period, eligible organization users receive an email.
- Each threshold is emailed at most once per monthly usage period.
- Alerts are not sent for unlimited organizations because a percentage of unlimited usage is undefined.
- Alerts do not block event ingestion.
- Existing monthly overage emails remain unchanged.

### Automatic smart project throttling

Exceptionless should automatically protect organization usage when one project starts sending a large event spike.

Supported behavior:

- Users should not be required to configure smart throttling.
- The system should avoid exposing a large number of throttling options.
- When throttling is needed, it should be scoped to the noisy project where possible rather than blocking all projects in the organization.
- While a project is throttled, Exceptionless should still accept a small sample of events from that project when the organization has remaining monthly allowance.
- The accepted sample should preserve troubleshooting visibility so users can tell whether the issue is still happening and whether a deployed fix helped.
- When smart throttling is applied, eligible organization users should receive an email notification.
- Smart throttling should use remaining monthly allowance and remaining time in the period, not only the static plan size, to avoid throttling customers who still have substantial monthly allowance left.

### Optional project event budgets

Users managing a project can configure a Project Event Budget from Project Settings → Usage.

Supported modes:

- **Off**: no project-specific cap; current behavior is preserved.
- **Fixed limit**: cap this project at a configured number of accepted events per monthly usage period.
- **Percentage of organization limit**: cap this project at a percentage of the organization's current monthly event allowance.

Examples:

- A project with no configured ingest limit can consume organization events as it does today.
- A project with a fixed limit of `20,000` can accept up to `20,000` events in the current monthly usage period, subject to the organization still having events available.
- A project with a `20%` limit in an organization with `100,000` monthly events has an effective project cap of `20,000`.
- If the organization plan changes, percentage-based project caps are recalculated from the organization's current event allowance.

When a project limit is reached:

- Event submissions for that project are limited or sampled.
- Event submissions for other projects in the organization are not limited solely because this project reached its cap.
- Blocked event usage is recorded for the project and organization.
- Existing organization overage behavior remains unchanged.

## Classification

- **Type:** Feature
- **Affected areas:** Backend/API, billing/usage enforcement, organization model, project model, Redis/cache usage counters, mail/work items, Svelte UI, generated API types, tests
- **OpenSpec justification:** This change affects event ingestion behavior, organization/project usage limits, smart throttling behavior, email notification behavior, API response contracts, persisted organization/project data, Redis/cache usage state, SDK/client expectations, jobs, and Svelte UI/API contracts.

## Current implementation context

Exceptionless currently enforces organization event overage in `OverageMiddleware` for event posts. The middleware checks the authenticated organization id, rejects disabled event submission, rejects oversized submissions, and calls `UsageService.GetEventsLeftAsync(organizationId)` before allowing the event post to continue.

`UsageService` already tracks both organization and project usage counters. `IncrementTotalAsync` increments organization totals and project totals using existing cache keys that accept an optional `projectId`.

`EventPostsJob` already parses queued event posts, calculates how many events may be processed, processes only allowed events, increments blocked usage for events over the limit, and increments total/discarded usage for processed events. This is the correct place to implement project-level sampled acceptance.

Exceptionless already has an organization overage email path. Existing usage overage publishes `PlanOverage`, which enqueues `OrganizationNotificationWorkItem`, and the work item handler sends organization notice emails to verified users with email notifications enabled. Budget alerts and project throttling notifications should reuse that mail/work-item pattern with distinct alert types rather than overloading monthly/hourly overage booleans.

The existing generic API throttling middleware remains separate. It limits API requests over a short period and is not the same as event-plan usage enforcement.

## Affected areas

### Backend/API

- Add organization usage budget alert settings to organization model/API responses.
- Add project event budget fields to project model/API responses.
- Extend usage tracking to publish budget-alert notifications when configured thresholds are crossed.
- Extend usage/event post processing to compute smart project throttling and sampled acceptance.
- Extend usage/event post processing to compute optional project budget limits.
- Preserve existing organization-level overage behavior and status codes.
- Avoid implementing project sampled throttling solely in middleware.

### Redis/cache

- Reuse existing organization/project usage counter keys.
- Add budget alert dedupe state keyed by organization, threshold, and monthly usage period.
- Add smart project throttling state or notification dedupe state keyed by organization/project/window where useful.
- Preserve existing usage counter TTL behavior.
- Do not add new project usage counter families unless required for efficient limit evaluation.

### Mail/work items

- Reuse the existing organization notification pattern.
- Add budget-alert-specific message/work item/template.
- Add project-smart-throttling-specific message/work item/template.
- Send emails only to verified users with email notifications enabled, matching existing organization notice behavior.

### Svelte UI

- Add organization budget alert controls to the Svelte Organization Settings → Usage page.
- Add project event budget controls to the Svelte Project Settings → Usage page.
- Surface smart throttling status where feasible, especially on Project Usage.
- Show computed thresholds/caps and current usage.
- Update the project usage chart to display the project limit when configured.

### Legacy Angular UI

- Legacy Angular parity is out of scope for this change unless the release path requires these settings to be available in the legacy UI.
- This change must not break existing legacy Angular organization or project management behavior.

### SDK/client compatibility

- Existing event submission authentication mechanisms must continue to work.
- Existing project API keys and user tokens must continue to work.
- Organization overage behavior must remain compatible.
- Smart project throttling for queued event posts should prefer asynchronous sampled processing over changing the HTTP submission response.
- Budget alert emails must be asynchronous side effects and must not change normal event submission success responses.

### Tests

- Add backend unit/integration tests for budget threshold calculation, smart throughput calculation, sampled acceptance, and project budget computation.
- Add usage service tests for threshold crossing, dedupe, organization cap, project cap, and smart throttling behavior.
- Add event post job tests for sampled acceptance and blocked usage accounting.
- Add API tests for organization budget alert and project event budget update/validation.
- Add mail/work-item tests for budget alert and smart throttling email eligibility/dedupe behavior.
- Add Svelte UI tests or targeted manual QA for the Usage page controls.
- Update HTTP samples if request/response contracts are changed.

## Compatibility risks

| Risk | Mitigation |
|------|------------|
| Existing event submissions could be rejected unexpectedly | Project event budgets default to null/Off for all existing projects. Smart throttling should preserve sampled acceptance when organization allowance remains. |
| Existing organization overage behavior could change | Keep organization-level monthly/bucket enforcement intact. Organization hard overage remains the outer limit. |
| Middleware-level project rejection would drop 100% of events | Project smart throttling and sampled acceptance must be implemented in the event processing path after events are parsed, not only in `OverageMiddleware`. |
| Too much configuration conflicts with product direction | Smart project throttling must work automatically with minimal or no required user configuration. |
| Existing customers expect some data while throttled | When the organization still has allowance, throttled projects should retain a small accepted sample rather than being fully blocked. |
| Static monthly plan throttling can feel unfair late in the month | Throughput calculations should consider events remaining in the monthly period and time remaining in the monthly period. |
| Project cap could be confused with reserved quota | UI copy must state that this is a cap, not a reservation. Other projects are not guaranteed unused capacity. |
| Percentage caps could behave unexpectedly when organization plans change | Effective percentage caps are recalculated from the current organization event allowance each time limits are evaluated. |
| Unlimited organization plans make percentage caps ambiguous | Percentage mode must be disabled or invalid for unlimited organizations. Fixed limits remain supported for project event budgets. Budget alert percentages are unavailable for unlimited organizations. |
| Budget alerts could send unexpected emails | Default budget alert settings to disabled for existing organizations. Users must opt in. |
| Duplicate threshold emails could annoy users | Each configured threshold is sent at most once per organization per monthly usage period. |
| Existing overage emails could change | Keep existing `PlanOverage` monthly/hourly emails unchanged and add budget alerts and smart throttling emails as separate notification types. |
| Persisted project or organization model changes could require Elasticsearch mapping/reindex | Store new settings source-only unless filtering/searching is added. No Elasticsearch mapping or reindex is required for enforcement. |

## Non-goals

- Do not implement API-key/token-level ingest limits in this change.
- Do not implement rate-based billing.
- Do not implement reserved project quotas.
- Do not change plan billing, invoices, Stripe integration, or organization plan limits.
- Do not change generic API request throttling behavior in `ThrottlingMiddleware`.
- Do not add new public event-submission endpoints.
- Do not migrate historical usage data.
- Do not require Elasticsearch reindexing.
- Do not require existing organizations or projects to be backfilled.
- Do not implement per-recipient custom alert thresholds in this change.
- Do not implement Slack/webhook budget alerts in this change.
- Do not implement stack-level adaptive throttling in v1 unless it can be done with low risk; project-level isolation is the v1 scope.
- Do not expose a large set of smart throttling tuning options in the UI.

## Future considerations

This design should leave room for future layers:

1. Organization plan/event allowance.
2. Organization budget alert thresholds.
3. Automatic smart project throttling.
4. Optional project event budget.
5. API key/token ingest cap.
6. Future rate-based or usage-based billing.

Budget alerts should leave room for future channels and scopes:

- fixed event-count alert thresholds
- project-specific budget alerts
- token/API-key-specific budget alerts
- Slack/webhook alert delivery
- role-based recipients
- rate-based billing alerts

The original issue suggested stack-level adaptive throttling with approximate occurrence counts. This change should leave room for that future direction, but v1 should focus on project-level isolation because it is simpler, aligns with project usage counters already tracked by Exceptionless, and directly addresses the customer problem of one product/environment affecting all others.

## Rollback plan

- Because project event budgets are opt-in, disabling or removing the UI leaves existing unset project budgets unchanged.
- Because budget alerts are opt-in, disabling or removing the UI leaves existing unset organization alert settings unchanged.
- If smart project throttling causes issues, smart throttling can be disabled while leaving budget alerts and optional project budgets in place.
- If budget alert emails cause issues, budget alert publishing/subscribers can be disabled while leaving stored settings in place.
- Existing organizations and projects default to null/Off, so rolling back model/API exposure has no effect on existing ingestion behavior unless users have already configured settings.
- If stored settings must be disabled quickly, project budget and budget alert evaluation can be short-circuited to treat all settings as unset.
