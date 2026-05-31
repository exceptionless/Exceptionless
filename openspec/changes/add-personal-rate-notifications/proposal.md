# Proposal: Add Personal Rate Notifications

## Summary

Add personal, rate-based project and stack notifications. Users can configure rules like:

- "Email me when this project has more than 100 errors in 5 minutes."
- "Email me when this stack occurs more than 20 times in 10 minutes."

The implementation is intentionally small and focused:

- Cheap cache-backed counters (1-minute UTC buckets)
- Asynchronous evaluator job
- Email delivery
- Cooldown and snooze to reduce noise
- Premium-gated runtime behavior that matches existing occurrence notifications
- Existing notification suppression semantics so muted traffic does not reappear as rate noise

This feature is informed by [issue #177](https://github.com/exceptionless/Exceptionless/issues/177), but intentionally does not implement the full notification wishlist. Issue #177's core goal — notifications should keep users informed without overwhelming them — is addressed through mandatory cooldowns, snooze support, and cache-only hot paths.

## Classification

- **Type:** Feature
- **Affected areas:**
  - Backend/API
  - Event pipeline
  - Cache/Redis
  - Queue jobs
  - Email
  - Svelte UI
  - Tests
- **OpenSpec justification:**
  - New API endpoints
  - New persisted rule model
  - New event-pipeline counter behavior
  - New evaluator/delivery jobs
  - Cross-cutting notification behavior
  - User-facing notification settings

## Goals

- Notify users when project or stack activity crosses a configured threshold.
- Detect high-volume repeated errors that current new/error/regression notifications can miss.
- Reduce notification noise versus per-event email streams.
- Keep event ingestion cheap.
- Support horizontal scaling with distributed cache and queues.
- Preserve current premium-only occurrence-notification behavior.
- Honor existing notification suppression so ignored, snoozed, discarded, and fixed stacks — and bot traffic already excluded from occurrence emails — do not generate rate alerts.
- Validate user/project/org state before sending.
- Support snoozing a noisy rule.
- Resume from a fresh baseline after snooze instead of back-alerting on traffic that happened while the rule was muted.
- Provide enough logging, metrics, and tests to trust the system.

## Non-goals

- Anomaly detection or machine learning
- Percent-change alerts
- Arbitrary query/filter language
- Tag rules
- Environment rules
- Time-of-day rules
- Quiet hours / quiet days
- Daily/weekly/monthly summary changes
- Digest emails
- No-data alerts
- Recovery/resolved notifications
- Notification grouping/digesting
- Generic webhook actions
- Slack actions (marked as future work)
- PagerDuty/OpsGenie integrations
- External recipients who are not Exceptionless users
- Mutating automated actions
- Action execution engine
- Durable action execution
- Rule history UI
- Delivery history UI
- In-app notification center
- Billing/overage notification changes
- Email sender/from-address overhaul
- Queue/system health notification UI
- Reduce-noise in-app callouts

## Deliberate Cutbacks

Issue #177 is broad — it covers configurable rules based on type, tags, time of day, number of exceptions, environment, snoozing, periodic digest emails, reduced-noise behavior, in-app notices, and third-party integrations.

This change intentionally implements only **personal rate notifications** — the minimum useful product:

> "When this project or stack exceeds X matching events in Y minutes, email me, but not more than once per cooldown, and let me snooze the rule."

The following are explicitly deferred:

- Digests and periodic summaries
- Quiet hours and time-of-day logic
- Advanced conditional rules (tags, environment, arbitrary filters)
- External recipients and non-user notification targets
- Webhooks, Slack, PagerDuty, and other delivery channels
- Mutating or automated actions
- Organization-level rules and inheritance

The first release should prove the cheap counter architecture and noise-control model before adding advanced alerting features.

## Compatibility Risks

| Risk | Mitigation |
|------|-----------|
| Existing project notification settings remain unchanged | Rate rules are stored separately; `Project.NotificationSettings` is not modified |
| Existing `EventNotificationsJob` behavior remains unchanged | Rate counters are a new pipeline action; existing notification queueing is unaffected |
| Existing `DailySummaryJob` behavior remains unchanged | No changes to daily summary logic |
| Existing Slack/webhook integrations are not changed | New delivery path is email-only; existing `WebHookNotification` queue untouched |
| Existing premium-only occurrence notification behavior could drift | Countering, evaluation, delivery, and Svelte enablement follow the same premium gating model as existing occurrence notifications |
| Rate counters depend on distributed cache in production | In-memory cache/queues remain development-only; production requires Redis/Azure providers |
| Muted stacks or bot traffic could reappear as new rate noise | Countering honors `Stack.AllowNotifications`, canceled/discarded contexts, and request-info bot markers before incrementing counters |
| Snooze could defer a notification instead of suppressing it | Evaluation resumes from a fresh baseline using the snooze boundary so activity gathered during snooze does not fire immediately on resume |
| Notification noise could increase if defaults are bad | Mandatory cooldowns, validation, and max rules per project (20) |
| Rules may become stale if user/project/org state changes | Delivery job re-validates rule, user, project, and org state before sending |
| Orphaned rules could be indexed and evaluated forever | Add cleanup on user membership changes and project/org deletion, plus cache invalidation for removed rules |

## Rollback Plan

1. Disable the rate notification evaluator job.
2. Disable the `UpdateRateCountersAction` in the event pipeline.
3. Existing event notifications and daily summaries continue to operate unchanged.
4. Delete or ignore persisted rate notification rules if needed.
5. Remove new UI route/feature module.
6. Remove new queues and cache keys if rolling back fully.
