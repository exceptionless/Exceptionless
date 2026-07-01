# Spec: Rate Notifications

## ADDED Requirements

### Requirement: User can list their project rate notification rules

The API MUST allow authenticated users to retrieve their own rate notification rules for a specific project.

#### Scenario: Authenticated user lists own rules

Given authenticated user with access to project
When GET `/api/v2/users/{userId}/projects/{projectId}/rate-notifications`
Then return only that user's rules for that project.

### Requirement: User can create a project rate notification rule

The API MUST allow authenticated users to create a rate notification rule scoped to a project with a threshold, window, cooldown, signal, and subject.

#### Scenario: Valid project rule is persisted

Given authenticated user with access to project
When POST valid rule with threshold, window, cooldown, signal, subject=Project
Then rule is persisted with organization_id, project_id, user_id, threshold, window, cooldown, signal, subject.

### Requirement: User can create a stack rate notification rule

The API MUST allow authenticated users to create a rate notification rule scoped to a specific stack within a project.

#### Scenario: Valid stack rule is persisted

Given authenticated user with access to project
And stack belongs to project
When POST subject=Stack and stack_id
Then rule is persisted.

### Requirement: Invalid stack scope is rejected

The API MUST reject a rule targeting a stack that does not belong to the specified project.

#### Scenario: Stack from another project is rejected

Given stack does not belong to project
When user creates stack rule
Then response is 400 or 404.

### Requirement: User cannot manage another user's rules

The API MUST deny non-admin users access to rate notification rules belonging to other users.

#### Scenario: Non-admin user is denied access to other user's rules

Given non-admin user A
When they request user B's rate notification rules
Then response is 404 or 403.

### Requirement: Global admin can manage another user's rules

The API MUST allow global administrators to create, read, update, and delete rate notification rules on behalf of any user.

#### Scenario: Admin accesses another user's rules

Given global admin
When they manage another user's rate notification rules
Then request succeeds.

### Requirement: Rule validation prevents noisy/invalid rules

The API MUST enforce validation constraints to prevent misconfigured or excessively noisy rules.

#### Scenario: Threshold must be positive

Given threshold <= 0
When user creates rule
Then response is 400 with validation error.

#### Scenario: Unsupported window is rejected

Given window is not one of 1m, 5m, 10m, 15m, 30m, 1h
When user creates rule
Then response is 400 with validation error.

#### Scenario: Cooldown shorter than window is rejected

Given cooldown < window
When user creates rule
Then response is 400 with validation error.

#### Scenario: Project subject with stack_id is rejected

Given subject = Project and stack_id is set
When user creates rule
Then response is 400 with validation error.

#### Scenario: Stack subject without stack_id is rejected

Given subject = Stack and stack_id is null
When user creates rule
Then response is 400 with validation error.

#### Scenario: Empty name is rejected

Given name is empty
When user creates rule
Then response is 400 with validation error.

#### Scenario: Name exceeding 100 characters is rejected

Given name length > 100
When user creates rule
Then response is 400 with validation error.

#### Scenario: Exceeding max rules per project is rejected

Given user already has 20 rules for project
When user creates another rule
Then response is 400 with validation error.

### Requirement: Event pipeline increments matching configured counters

The event pipeline MUST increment the corresponding rate counter in the current UTC minute bucket when an event matches an enabled rule's signal.

#### Scenario: Matching event increments counter

Given enabled project rule exists
When matching event is processed
Then matching rate counter for current UTC minute bucket is incremented.

### Requirement: Event pipeline skips projects without enabled rules

The event pipeline MUST NOT perform any counter operations for projects with no enabled rate notification rules.

#### Scenario: No rules means no counter work

Given no enabled rate notification rules for project
When event is processed
Then no rate counter is incremented.

### Requirement: Rate notifications honor premium feature gating

Rate notifications MUST follow the existing premium-only occurrence-notification model and MUST NOT become a free notification channel.

#### Scenario: Non-premium organizations do not activate rate notifications

Given project organization does not have premium features
When matching events are processed or the evaluator runs
Then no rate counters are incremented
And no rate notification work item is enqueued
And no rate notification email is sent.

### Requirement: Event pipeline increments only required counters

The pipeline MUST only increment counters that are required by at least one enabled rule, not all possible signal counters.

#### Scenario: Only configured signal counters are incremented

Given project has only an Errors rule
When critical error event is processed
Then Errors counter is incremented
And unrelated counters are not incremented unless required by configured rules.

### Requirement: Stack counters are stack-specific

Stack-scoped counters MUST only be incremented by events on the specific stack referenced by the rule.

#### Scenario: Event on different stack does not increment

Given stack rule exists for stack A
When event occurs on stack B
Then stack A counter is not incremented.

### Requirement: Rate notifications honor existing occurrence-notification suppression

Rate notifications MUST respect existing occurrence-notification suppression semantics so muted traffic does not reappear as rate noise.

#### Scenario: Events on muted stacks do not increment counters

Given the event stack has `AllowNotifications = false`
When the event is processed
Then no rate counter is incremented for that event.

#### Scenario: Bot-marked requests do not increment counters

Given request enrichment has marked the event request as a bot
When the event is processed
Then no rate counter is incremented for that event.

### Requirement: Evaluator enqueues notification when threshold is crossed

The evaluator job MUST enqueue a RateNotification work item when the sum of counter buckets for a rule's window meets or exceeds the threshold.

#### Scenario: Threshold reached triggers enqueue

Given rule threshold is 100 errors in 5 minutes
And matching counters sum to 100 or more
When evaluator runs
Then RateNotification work item is enqueued.

### Requirement: Evaluator does not enqueue below threshold

The evaluator MUST NOT enqueue a notification when the observed event count is below the rule threshold.

#### Scenario: Below threshold is silent

Given rule threshold is 100
And observed count is 99
When evaluator runs
Then no work item is enqueued.

### Requirement: Cooldown suppresses repeated sends

The evaluator MUST NOT enqueue a new notification for a rule until the cooldown period from the previous firing has expired.

#### Scenario: Active cooldown prevents re-enqueue

Given rule has fired
And cooldown has not expired
When threshold is crossed again
Then no new work item is enqueued.

### Requirement: Snooze suppresses sends

The evaluator MUST NOT fire a snoozed rule until the snooze period expires.

#### Scenario: Snoozed rule does not fire

Given rule snoozed_until_utc is in the future
When threshold is crossed
Then no work item is enqueued.

### Requirement: Snooze resumes from a fresh baseline

When a snoozed rule resumes, the evaluator MUST ignore activity observed entirely during the snooze window so the rule does not back-alert immediately after unsnooze or natural expiry.

#### Scenario: Unsnoozing does not immediately fire on snoozed activity

Given a rule was snoozed while matching events continued
And the shared subject counter remained active for another enabled rule
When the user unsnoozes the rule
Then the evaluator does not enqueue a rate notification until new post-unsnooze activity crosses the threshold.

#### Scenario: Snooze expiry does not immediately fire on snoozed activity

Given a rule remained snoozed until its snooze window expired
And matching activity during the snooze window crossed the threshold
When the evaluator next runs after the snooze expires
Then the evaluator does not enqueue a rate notification until new post-expiry activity crosses the threshold.

### Requirement: Disabled rules do not fire

The evaluator MUST skip disabled rules during evaluation.

#### Scenario: Disabled rule is skipped

Given rule is disabled
When threshold is crossed
Then no work item is enqueued.

### Requirement: No healthy/no-activity emails

The system MUST NOT send notifications indicating that everything is fine or that no activity occurred.

#### Scenario: Below threshold produces no notification

Given threshold is not crossed
When evaluator runs
Then no healthy/no-activity notification is sent.

### Requirement: Delivery sends email for valid notification

The delivery job MUST send a rate notification email when the rule, project, user, and email state are all valid.

#### Scenario: Valid state delivers email

Given RateNotification work item
And rule/project/user are valid
And user email is verified
And user email notifications are enabled
When delivery job processes item
Then rate notification email is queued.

### Requirement: Delivery skips invalid user state

The delivery job MUST skip sending and log structured reasons when any precondition is not met.

#### Scenario: Unverified email is skipped

Given user email is unverified
When delivery job processes item
Then notification is skipped with log.

#### Scenario: Notifications disabled is skipped

Given user email notifications are disabled
When delivery job processes item
Then notification is skipped with log.

#### Scenario: User not in organization is skipped

Given user no longer belongs to organization
When delivery job processes item
Then notification is skipped with log.

#### Scenario: Deleted project is skipped

Given project is deleted
When delivery job processes item
Then notification is skipped with log.

#### Scenario: Deleted rule is skipped

Given rule is deleted
When delivery job processes item
Then notification is skipped with log.

#### Scenario: Disabled rule is skipped during delivery

Given rule is disabled
When delivery job processes item
Then notification is skipped with log.

#### Scenario: Stale rule version is skipped

Given rule version does not match work item
When delivery job processes item
Then notification is skipped with log.

### Requirement: Email includes actionable context

Rate notification emails MUST include all information the user needs to understand and act on the alert.

#### Scenario: Email body contains all required fields

Given notification email is queued
Then email includes rule name, project name, observed count, threshold, window, subject type, stack title (when applicable), link to project or stack, and cooldown explanation.

#### Scenario: Stack-scoped email includes stack context

Given a stack-scoped rate notification email is queued
When the delivery job loads the stack context
Then the email includes the stack title and a deep link to the stack.

### Requirement: User can manage project rate rules in Svelte UI

The Svelte UI MUST provide a full CRUD interface for rate notification rules within project settings.

#### Scenario: Full CRUD and controls available

Given user opens project notification settings
Then they can list, create, edit, delete, enable/disable, snooze, and unsnooze rate rules.

#### Scenario: Non-premium organizations show rate notifications as unavailable

Given the organization does not have premium features
When the user opens project notification settings
Then the UI shows the feature as unavailable
And the user cannot create or enable active rate notification rules.

### Requirement: UI avoids advanced/deferred features

The v1 UI MUST NOT expose features that are deferred to future iterations.

#### Scenario: Deferred features are not exposed

Given user opens rate notification management UI
Then UI does not expose digests, webhooks, automated actions, quiet hours, arbitrary filters, external recipients, or no-data alerts.

### Requirement: Rate notification hot path is cache-only

The event pipeline counter path MUST use only cache operations and MUST NOT query Elasticsearch per event.

#### Scenario: No Elasticsearch per event

Given event is processed
Then rate notification countering does not query Elasticsearch per event.

### Requirement: Production requires distributed cache/queue

Production deployments MUST use provider-backed (Redis/Azure/SQS) cache and queues for rate notification infrastructure.

#### Scenario: Production uses provider-backed infrastructure

Given production deployment
Then rate notification counters and queues use provider-backed cache/queues, not in-memory providers.

### Requirement: Rollback does not affect existing notifications

Disabling rate notification components MUST NOT impact existing event notification or daily summary behavior.

#### Scenario: Disabling rate notifications leaves existing behavior intact

Given rate notification evaluator/action is disabled
Then existing event notifications and daily summaries continue to operate unchanged.

### Requirement: Rule lifecycle cleanup removes orphaned rules

The system MUST remove or invalidate orphaned rate notification rules when the owning membership, project, or organization is deleted.

#### Scenario: Membership removal cleans up user rules

Given a user is removed from the organization
When cleanup runs
Then that user's rate notification rules for the organization are removed or invalidated
And cached rule indexes are invalidated.

#### Scenario: Project deletion cleans up project rules

Given a project is deleted
When cleanup runs
Then rate notification rules for that project are removed or invalidated
And cached rule indexes are invalidated.
