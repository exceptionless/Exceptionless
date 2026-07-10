# Tasks: Add Personal Rate Notifications

## Backend - Models and Repositories

- [x] **Add RateNotificationRule model and enums**
  - Create `RateNotificationRule` model with all fields (Id, OrganizationId, ProjectId, UserId, Version, Name, IsEnabled, Signal, Subject, StackId, Threshold, Window, Cooldown, SnoozedUntilUtc, LastFiredUtc, CreatedUtc, UpdatedUtc, IsDeleted)
  - Create `RateNotificationSignal` enum (AllEvents, Errors, CriticalErrors, NewErrors, Regressions)
  - Create `RateNotificationSubject` enum (Project, Stack)

- [x] **Add IRateNotificationRuleRepository and implementation**
  - Create interface extending `IRepositoryOwnedByOrganizationAndProject<RateNotificationRule>`
  - Add methods: `GetByProjectIdAndUserIdAsync`, `GetEnabledByProjectIdAsync`, `CountByProjectIdAndUserIdAsync`
  - Create Elasticsearch-backed implementation following existing repository patterns

- [x] **Register repository in DI**
  - Register `IRateNotificationRuleRepository` / `RateNotificationRuleRepository` in `Bootstrapper.cs`

## Backend - Countering and Evaluation

- [x] **Add compiled RateNotificationRuleCache service**
  - Load all enabled rules for a project with search-after pagination
  - Cache unique compiled project/stack counters grouped by signal with short TTL
  - Invalidate through the hybrid cache on rule-management changes and lifecycle cleanup without churning the plan for evaluator bookkeeping
  - Cache key: `rate:v2:counter-plan:project:{projectId}`

- [x] **Add RateCounterService**
  - Implement 1-minute UTC bucket counters via `ICacheClient`
  - Methods: increment counter, sum buckets for window, check/set cooldown
  - Counter key format: `rate:v1:count:{epochMinute}:{counterKey}`
  - Active bucket tracking: `rate:v1:active:{epochMinute}`
  - Cooldown key format: `rate:v1:cooldown:{ruleId}:{subjectKey}`
  - TTLs: counter/active = 3h, cooldown = configured cooldown

- [x] **Add UpdateRateCountersAction (event pipeline)**
  - Priority after stack assignment (e.g., 75)
  - Exit fast if organization lacks premium features or the rollout flag
  - Load rule index for project; exit fast if no enabled rules
  - Skip events on stacks where `AllowNotifications` is false
  - Skip canceled/discarded events and requests already marked as bots
  - Match event against compiled counter definitions (signal matching)
  - Perform N unique counter increments plus one batched active-key update
  - Never query Elasticsearch per event; never send notifications directly

- [x] **Add RateNotificationEvaluatorJob**
  - Periodic job (60s interval) with distributed lock
  - Skip organizations without premium features or the rollout flag
  - Load all enabled rules with search-after pagination
  - Inspect recently active counters
  - Sum buckets for each rule's window using a fresh baseline after snooze/unsnooze
  - Skip disabled/snoozed rules
  - Compare observed ≥ threshold; enforce cooldown per rule+subject
  - Enqueue `RateNotification` work item on threshold crossing
  - Set cooldown key on successful enqueue
  - Structured logging for fired/skipped reasons

- [x] **Add RateNotification queue model**
  - Fields: RuleId, RuleVersion, OrganizationId, ProjectId, UserId, SubjectKey, StackId, WindowStartUtc, WindowEndUtc, ObservedCount, Threshold

- [x] **Register counter/evaluator services in DI**
  - Register `RateNotificationRuleIndex`, `RateCounterService`, `RateNotificationEvaluatorJob`
  - Register `IQueue<RateNotification>` in Core and Insulation bootstrappers

## Backend - Delivery

- [x] **Add RateNotificationsJob (delivery)**
  - Load rule, project, user
  - Load stack for stack-scoped rules
  - Validate: rule exists, enabled, version compatible, user in org, email verified, notifications enabled, project/org exists
  - Send email via `IMailer.SendRateNotificationAsync`
  - Skip with structured logs on validation failure

- [x] **Add IMailer.SendRateNotificationAsync**
  - Add method to `IMailer` interface and `Mailer` implementation
  - Email includes: rule name, project name, observed count, threshold, window, subject type, stack title (when applicable), link, cooldown explanation
  - Subject format: `[ProjectName] Error rate exceeded`
  - No "everything is fine" messaging

- [x] **Update Insulation queue registration**
  - Register `IQueue<RateNotification>` for Redis/Azure/SQS providers in `Exceptionless.Insulation/Bootstrapper.cs`

## Backend - API

- [x] **Add RateNotificationRuleController**
  - Routes: GET list, POST create, GET by id, PUT update, DELETE, POST snooze, POST unsnooze
  - Authorization: current user manages own rules; global admin manages any user's rules; user must access project org
  - Validation: defined enums, threshold > 0, supported windows, window ≤ cooldown ≤ 24h, subject/stack consistency, max 20 rules per user per project, name non-empty and ≤ 100 chars
  - Preserve persisted rules when premium or the rollout flag is removed, but keep runtime and Svelte behavior inactive

- [x] **Add request/response DTOs**
  - Create/update request models with validation attributes
  - Snooze request model (duration or until timestamp)

- [x] **Update tests/http files**
  - Add HTTP sample requests for all rate notification endpoints

## Frontend

- [x] **Add rate-notifications feature module**
  - Create `src/Exceptionless.Web/ClientApp/src/lib/features/rate-notifications/`
  - Models/types matching API DTOs
  - TanStack Query API wrappers (list, create, update, delete, snooze, unsnooze)

- [x] **Add rate notification rule list component**
  - List rules for current user/project
  - Show enable/disable toggle, snooze status
  - Hide the feature unless the project exposes the combined premium-plus-rollout capability
  - Delete confirmation

- [x] **Add rate notification rule form component**
  - Fields: Name, Signal, Subject, Stack selector, Threshold, Window, Cooldown, Enabled
  - Validation matching backend constraints
  - Noise warning copy: "This rule may be noisy. Use a cooldown to avoid repeated emails."

- [x] **Integrate into project settings**
  - Add rate notifications section/tab in project notification settings
  - Gate the section with additive `ViewProject.has_rate_notifications`

## Tests

- [x] **Add unit tests**
  - Rule validation (threshold, window, cooldown, subject/stack, name)
  - Counter key builder
  - Counter increments and bucket summing
  - Signal matching logic
  - Premium gating
  - `AllowNotifications` / bot suppression
  - Cooldown behavior
  - Snooze behavior
  - Fresh-baseline behavior after snooze/unsnooze
  - Evaluator threshold crossing

- [x] **Add integration tests**
  - User CRUD own rules (list, create, get, update, delete)
  - User cannot manage another user's rules
  - Global admin can manage another user's rules
  - Stack rule rejects stack from another project
  - Non-premium organizations do not counter, evaluate, or deliver rate notifications
  - Events on stacks with `AllowNotifications = false` do not increment rate counters
  - Bot-marked requests do not increment rate counters
  - Evaluator enqueues when threshold crossed
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

- [x] **Add lifecycle cleanup**
  - Remove/invalidate rules when user membership changes
  - Remove/invalidate rules when project or organization is deleted

## Validation

- [x] **Run OpenSpec validation**
  - `openspec validate add-personal-rate-notifications --strict --no-interactive`

- [x] **Run relevant builds and tests**
  - `dotnet build`
  - `dotnet test`
  - Frontend build and lint
