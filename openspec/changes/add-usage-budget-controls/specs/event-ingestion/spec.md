# Spec: Event Ingestion

## ADDED Requirements

### Requirement: Smart throttling works without required user configuration

Exceptionless must provide automatic smart project throttling without requiring users to configure per-project or per-stack throttling settings.

#### Scenario: No smart throttling configuration exists

Given an organization has projects
And no smart throttling settings have been configured
When one project starts sending a large event spike
Then Exceptionless should still be able to apply smart throttling automatically.

#### Scenario: UI does not require many throttling options

Given smart throttling is available
When a user views organization or project usage settings
Then the UI must not require the user to configure many low-level throttling parameters before smart throttling can protect the organization.

### Requirement: Smart throttling uses remaining allowance and remaining time

Smart throttling must calculate allowed throughput using the organization's remaining monthly event allowance and the time remaining in the monthly usage period.

#### Scenario: Organization has substantial allowance remaining late in the month

Given an organization is late in the monthly usage period
And the organization still has substantial event allowance remaining
When smart throttling calculates allowed throughput
Then the calculation must account for remaining event allowance
And must not rely only on the static monthly plan size divided by the original month duration.

#### Scenario: Organization has little allowance remaining

Given an organization has little event allowance remaining
When smart throttling calculates allowed throughput
Then the allowed throughput should decrease to protect the remaining monthly allowance.

### Requirement: Smart throttling isolates noisy projects where possible

When one project is responsible for a usage spike, smart throttling must apply to that project where possible rather than fully throttling the entire organization.

#### Scenario: One project spikes

Given an organization has Project A and Project B
And Project A sends a large event spike
And Project B remains within normal usage
When smart throttling is applied
Then Project A may be throttled or sampled
And Project B must not be blocked solely because Project A spiked.

#### Scenario: Multiple projects spike

Given an organization has multiple projects
And more than one project exceeds smart throttling criteria
When smart throttling is applied
Then each noisy project may be evaluated and throttled independently.

### Requirement: Smart throttling preserves a sample of events

When a project is smart-throttled and the organization still has remaining monthly allowance, Exceptionless must continue accepting a small sample of events for that project.

#### Scenario: Project is throttled but organization has allowance

Given an organization has remaining monthly event allowance
And a project is under smart throttling
When events are submitted for that project
Then Exceptionless must accept a small sample of events
And must block or discard the remainder according to usage accounting rules.

#### Scenario: Sample rate remains small

Given a project is under smart throttling
When the accepted sample is calculated
Then the accepted sample should be within the intended 1 to 5 percent range
Unless a lower organization or project hard limit leaves fewer events available.

#### Scenario: Organization allowance exhausted

Given an organization has no remaining monthly event allowance
And a project is under smart throttling
When events are submitted for that project
Then existing organization overage behavior must apply
And smart throttling must not allow sampled events beyond the organization hard allowance.

### Requirement: Smart throttling operates after event posts are parsed

Smart project throttling must be evaluated in a processing path that knows the number of events in the post so it can accept a sample and block the remainder.

#### Scenario: Batch contains more events than allowed

Given a queued event post contains many events
And smart throttling allows only a sample
When the event post is processed
Then the processor must process only the sampled/allowed events
And must record blocked usage for the rest.

#### Scenario: Full request rejection would prevent sampling

Given an event post would otherwise be rejected entirely by project-level throttling
When the organization still has remaining allowance
Then the system should prefer sampled processing over full request rejection.

### Requirement: Smart throttling sends project throttling notification

When smart throttling is applied to a project, Exceptionless must send an email notification to eligible organization users.

#### Scenario: Project enters smart-throttled state

Given a project was not previously smart-throttled
And smart throttling is applied to the project
When notification processing runs
Then eligible organization users must receive a project throttling notification email.

#### Scenario: Project remains smart-throttled

Given a project is already smart-throttled
When additional event posts are processed while the project remains throttled
Then Exceptionless must not send duplicate throttling emails for every post.

#### Scenario: User is not email-eligible

Given a user belongs to the organization
And the user does not have a verified email address or has email notifications disabled
When a project throttling notification is sent
Then that user must not receive the email.

### Requirement: Smart throttling usage accounting remains accurate

Smart throttling must record accepted, blocked, and discarded event counts consistently with existing usage accounting.

#### Scenario: Sampled events accepted

Given a project is smart-throttled
When a sample of events is accepted
Then accepted organization and project usage must increase by the number of processed events.

#### Scenario: Non-sampled events blocked

Given a project is smart-throttled
When some events in a post are not accepted due to throttling
Then blocked usage must increase by the number of non-accepted events.

#### Scenario: Budget alerts use accepted events only

Given budget alerts are enabled and a project is smart-throttled
When non-sampled events are blocked
Then blocked events must not count as accepted usage for organization budget alert thresholds.

### Requirement: Smart throttling does not replace explicit project budgets

Automatic smart throttling must coexist with optional project event budgets.

#### Scenario: Project has no explicit budget

Given a project has no configured project event budget
When the project spikes
Then automatic smart throttling may still apply.

#### Scenario: Project has explicit budget

Given a project has an explicit project event budget and automatic smart throttling also applies
When events are processed
Then the number of events accepted must not exceed the lowest applicable allowance from organization hard limit, project budget, and smart throttling sample.

## ADDED Requirements

### Requirement: Projects MUST be able to define optional event budgets

Exceptionless projects MUST be able to define an optional project-level event budget that caps or limits the number of accepted events for that project within the current monthly usage period.

#### Scenario: Project has no event budget

Given a project has no configured event budget and the organization has remaining event allowance
When an event is submitted for the project
Then the explicit project event budget must not block the event
And existing organization-level usage enforcement and automatic smart throttling must apply.

#### Scenario: Existing projects remain uncapped

Given a project existed before project event budgets were introduced
When the project is loaded
Then its project event budget must be treated as unset.

### Requirement: Fixed project event budgets MUST cap accepted project events

A fixed project event budget MUST cap accepted event volume for a project to a configured positive integer for the current monthly usage period.

#### Scenario: Fixed project budget allows events below cap

Given an organization has remaining event allowance and a project has a fixed event budget of 20000 events
And the project has accepted fewer than 20000 events in the current monthly usage period
When an event is submitted for the project
Then the event must not be blocked by the explicit project event budget.

#### Scenario: Fixed project budget limits events at cap

Given an organization has remaining event allowance and a project has a fixed event budget of 20000 events
And the project has accepted 20000 events in the current monthly usage period
When events are submitted for the project
Then accepted events for that project must be limited by the project event budget
And blocked usage must be recorded for non-accepted events.

#### Scenario: Fixed project budget above organization allowance is clamped

Given an organization has a finite monthly event allowance of 10000 events
And a project has a fixed event budget of 20000 events
When the effective project event budget is evaluated
Then the effective project event budget must be 10000 events.

#### Scenario: Fixed project budget on unlimited organization

Given an organization has unlimited event allowance and a project has a fixed event budget of 20000 events
When the effective project event budget is evaluated
Then the effective project event budget must be 20000 events.

### Requirement: Percentage project event budgets MUST derive from organization allowance

A percentage project event budget MUST cap accepted event volume for a project to a percentage of the organization's current finite monthly event allowance.

#### Scenario: Percentage project budget computes effective cap

Given an organization has a finite monthly event allowance of 100000 events
And a project has a percentage event budget of 20 percent
When the effective project event budget is evaluated
Then the effective project event budget must be 20000 events.

#### Scenario: Percentage project budget on unlimited organization is inactive

Given an organization has unlimited event allowance and a project has a percentage event budget
When the effective project event budget is evaluated
Then the percentage project event budget must not produce an effective project cap.

### Requirement: Project event budgets MUST be caps, not reservations

Project event budgets MUST prevent a project from exceeding its cap but must not reserve organization event allowance for other projects.

#### Scenario: Unused project budget does not reserve events

Given an organization has a monthly event allowance of 100000 events
And Project A has a percentage event budget of 20 percent and uses 0 events
When Project B submits events
Then Project B must not be blocked solely because Project A has unused project budget.

#### Scenario: Project budget does not increase organization allowance

Given an organization has no remaining event allowance and a project has remaining project event budget
When an event is submitted for the project
Then the event must be rejected due to organization overage.

### Requirement: Project event budget exhaustion MUST limit only the capped project

When one project reaches its project event budget, other projects in the same organization MUST continue to be evaluated independently.

#### Scenario: One capped project does not block another project

Given an organization has remaining event allowance
And Project A has reached its project event budget and Project B has no project event budget
When an event is submitted for Project B
Then the event must not be blocked because Project A reached its budget.

#### Scenario: Multiple projects have independent budgets

Given an organization has remaining event allowance
And Project A has a fixed event budget of 1000 events and Project B has a fixed event budget of 2000 events
When Project A reaches 1000 accepted events
Then Project A must be limited by its project event budget
And Project B must continue to be evaluated against its own 2000-event budget.

### Requirement: Organization overage MUST remain the hard outer limit

Organization-level event allowance MUST remain the hard billing/usage boundary.

#### Scenario: Organization overage takes precedence

Given an organization has no remaining event allowance and a project has remaining project event budget
When an event is submitted for the project
Then the event must be rejected due to organization overage.

### Requirement: Project ingest controls MUST reuse existing usage accounting

Project budget and smart throttling enforcement MUST use existing project usage counters.

#### Scenario: Accepted event increments project usage

Given an organization has remaining event allowance and a project has remaining project event budget
When an event is accepted for the project
Then existing project accepted event usage must be incremented.

#### Scenario: Blocked event does not count as accepted project usage

Given a project has reached its project event budget
When an event is submitted for the project
Then accepted project usage must not increase for non-accepted events
And blocked usage must be recorded.

### Requirement: Project controls MUST support safe defaults on failure

Project budget and smart throttling evaluation MUST avoid introducing new ingestion failures when project data is missing or invalid.

#### Scenario: Missing project does not cause project-budget rejection

Given an event submission has an organization id and the project cannot be loaded
When project controls are evaluated
Then the system must not reject the event solely due to project budget evaluation.

#### Scenario: Invalid persisted project budget is treated as inactive

Given a project has an invalid persisted event budget configuration
When event ingestion evaluates project controls
Then the invalid project budget must be treated as inactive.

### Requirement: Project ingest controls MUST prefer sampled processing over full request rejection

Project-level ingest controls MUST preserve sampled visibility where possible.

#### Scenario: Queued event post contains many project events

Given an event post has been queued and the project is over its smart throttling allowance
And the organization still has remaining allowance
When the event post is processed
Then the job should process an allowed sample and record blocked usage for non-sampled events.

#### Scenario: Middleware cannot sample project events

Given a project-level condition would require preserving only 1 to 5 percent of events
When the request is still in middleware before parsing
Then middleware must not be the only enforcement layer.
