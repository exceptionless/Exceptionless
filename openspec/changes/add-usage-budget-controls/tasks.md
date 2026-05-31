# Tasks: Add Usage Budget Controls

## OpenSpec

* Task 1: Create OpenSpec change
    * Create proposal, design, tasks, and spec deltas for:
        * organization budget alerts
        * automatic smart project throttling
        * project event budgets
        * API compatibility
        * organization/project auth behavior
    * Verification:
        * openspec validate add-usage-budget-controls --strict --no-interactive

## Backend model and API contract

* Task 2: Add organization budget alert settings model
    * Add OrganizationBudgetAlertSettings.
    * Add nullable BudgetAlertSettings to Organization.
    * Add validation for enabled settings and percentage thresholds.
    * Verification:
        * dotnet build
        * Add model/validation tests if existing model validation tests are available.
* Task 3: Add budget alert settings to organization API contract
    * Add BudgetAlertSettings to ViewOrganization.
    * Prefer adding UpdateOrganization with Name and BudgetAlertSettings.
    * Update OrganizationController generic update DTO if using UpdateOrganization.
    * Regenerate generated API and Svelte schemas.
    * Verification:
        * dotnet build
        * dotnet test -- --filter-class OrganizationControllerTests
        * cd src/Exceptionless.Web/ClientApp && npm ci && npm run check
* Task 4: Add project event budget domain model
    * Add ProjectIngestLimit and ProjectIngestLimitType under src/Exceptionless.Core/Models/.
    * Add nullable Project.IngestLimit.
    * Add validation for fixed and percentage modes.
    * Verification:
        * dotnet build
        * Add/adjust serializer tests if project model serialization coverage exists.
* Task 5: Add project event budget to project DTOs
    * Add ProjectIngestLimit? IngestLimit to UpdateProject.
    * Add ProjectIngestLimit? IngestLimit to ViewProject.
    * Add int? EffectiveIngestLimit to ViewProject.
    * Add smart throttling state fields to ViewProject if feasible:
        * bool IsSmartThrottled
        * double? SmartThrottleSampleRate
    * Ensure Mapperly maps the new fields or add explicit mapping if required.
    * Verification:
        * dotnet build
        * dotnet test -- --filter-class ProjectControllerTests
* Task 6: Regenerate OpenAPI and Svelte generated types
    * Regenerate src/Exceptionless.Web/ClientApp/src/lib/generated/api.ts.
    * Regenerate src/Exceptionless.Web/ClientApp/src/lib/generated/schemas.ts.
    * Confirm UpdateOrganization, ViewOrganization, UpdateProject, ViewProject, and validation schemas include new fields.
    * Verification:
        * dotnet build
        * cd src/Exceptionless.Web/ClientApp && npm ci && npm run check

## Organization budget alert emails

* Task 7: Add budget alert threshold calculation
    * Use the same effective organization allowance as existing usage enforcement.
    * Support configured percentage thresholds from 1 through 99.
    * Treat thresholds as inactive for unlimited organizations.
    * Verification:
        * Add tests to UsageServiceTests.
        * dotnet test -- --filter-class UsageServiceTests
* Task 8: Publish budget alert messages when thresholds are crossed
    * Add OrganizationBudgetAlert message.
    * In UsageService.IncrementTotalAsync, detect threshold crossings after accepted event usage increments.
    * Publish one message per crossed threshold.
    * Do not publish repeatedly after threshold was already sent in the current monthly usage period.
    * Verification:
        * Tests:
            * below threshold does not publish
            * crossing threshold publishes
            * already-sent threshold does not publish
            * batch crossing multiple thresholds publishes each crossed threshold
        * dotnet test -- --filter-class UsageServiceTests
* Task 9: Add budget alert dedupe
    * Add dedupe key per organization, threshold, and monthly usage period.
    * Set TTL through the end of the monthly usage period plus a safety buffer.
    * Prefer non-blocking behavior if dedupe cache fails.
    * Verification:
        * Add tests for one-email-per-threshold-per-period behavior.
        * dotnet test -- --filter-class UsageServiceTests
* Task 10: Add budget alert work item and subscriber
    * Add OrganizationBudgetAlertWorkItem.
    * Add startup subscriber that turns OrganizationBudgetAlert messages into work items.
    * Add work item handler that loads organization/users and sends emails.
    * Re-check current organization budget alert settings before sending.
    * Verification:
        * dotnet test -- --filter-class OrganizationBudgetAlertWorkItemHandlerTests
        * If no dedicated handler test class exists, add one.
* Task 11: Add budget alert mailer method and template
    * Add IMailer.SendOrganizationBudgetAlertAsync.
    * Implement the method in Mailer.
    * Add organization-budget-alert.html template.
    * Include current usage, threshold percentage, threshold event count, event limit, remaining events, and links to organization usage/billing.
    * Verification:
        * Mailer/template rendering test if existing template tests exist.
        * dotnet build

## Automatic Smart Project Throttling

* Task 12: Add smart throughput calculation
    * Calculate allowed throughput from events left in the monthly period and time/windows left in the period.
    * Preserve existing burst tolerance behavior where appropriate.
    * Avoid static plan-size-only calculations.
    * Verification:
        * Tests for early-month, late-month, high-remaining, and low-remaining allowance cases.
        * dotnet test -- --filter-class UsageServiceTests
* Task 13: Add project-level smart throttling decision
    * Detect projects contributing to usage spikes.
    * Apply smart throttling to noisy projects where possible instead of organization-wide full blocking.
    * Verification:
        * Tests where Project A spikes and Project B remains unaffected.
        * Tests where multiple projects are evaluated independently.
        * dotnet test -- --filter-class UsageServiceTests
* Task 14: Add sampled acceptance for smart-throttled projects
    * Implement 1–5% sampled acceptance when project is smart-throttled and organization has remaining allowance.
    * Prefer deterministic/testable sampling.
    * Record blocked usage for non-sampled events.
    * Verification:
        * Tests for sampled acceptance count.
        * Tests that blocked usage increments for non-sampled events.
        * Tests that accepted usage increments only for sampled events.
        * dotnet test -- --filter-class EventPostsJobTests
* Task 15: Move project-level sampled enforcement into event post processing
    * Extend EventPostsJob / UsageService after event post parsing to calculate allowed events.
    * Avoid using only OverageMiddleware for project-level sampled enforcement.
    * Preserve existing organization hard overage middleware behavior.
    * Verification:
        * Tests for batch event posts where only a sample is processed.
        * Tests for organization hard overage still blocking.
        * dotnet test -- --filter-class EventPostsJobTests
        * dotnet test -- --filter-class OverageMiddlewareTests
* Task 16: Add smart throttling notification message/work item/email
    * Add project throttling notification message.
    * Add work item and handler.
    * Add mailer method and template.
    * Send to verified users with email notifications enabled.
    * Deduplicate notifications during a throttling cooldown window.
    * Verification:
        * dotnet test -- --filter-class ProjectSmartThrottleNotificationWorkItemHandlerTests
        * dotnet build

## Optional project event budgets

* Task 17: Refactor UsageService events-left calculation
    * Extract organization-only events-left logic into a helper that accepts optional projectId.
    * Reuse existing project-aware cache key helpers.
    * Preserve current organization behavior.
    * Verification:
        * Add tests for organization-only events-left behavior to ensure no regression.
        * dotnet test -- --filter-class UsageServiceTests
* Task 18: Add project effective budget calculation
    * Implement effective fixed budget calculation.
    * Implement effective percentage budget calculation.
    * Clamp fixed budgets to finite organization allowance during enforcement.
    * Treat percentage budgets as inactive when organization allowance is unlimited.
    * Verification:
        * dotnet test -- --filter-class UsageServiceTests
* Task 19: Add parsed-event ingest allowance result
    * Add EventIngestAllowanceResult.
    * Add UsageService.GetEventIngestAllowanceAsync(string organizationId, string projectId, int submittedEventCount) or equivalent.
    * Return event count allowed, organization remaining, project remaining, effective project budget, and smart throttling state.
    * Verification:
        * Tests:
            * no project id or missing project falls back safely
            * no project budget uses organization + smart throttling
            * fixed budget limits allowed event count
            * percentage budget computes effective cap from organization allowance
            * organization overage takes precedence over project controls
            * smart throttling sample and project budget combine by taking the minimum allowed count
        * dotnet test -- --filter-class UsageServiceTests

## Middleware and event processing

* Task 20: Keep OverageMiddleware as coarse gate
    * Preserve existing status codes for organization overage, disabled submission, missing content length, oversized posts, and unauthorized organization context.
    * Do not implement sampled project enforcement only in middleware.
    * Verification:
        * dotnet test -- --filter-class OverageMiddlewareTests
* Task 21: Update EventPostsJob for sampled/project-budget allowance
    * After parsing events, ask UsageService for allowed event count.
    * Process only allowed events.
    * Use deterministic sampling/selection when allowed count is lower than submitted count due to smart throttling.
    * Increment blocked usage for non-accepted events.
    * Increment accepted usage only for processed events.
    * Verification:
        * dotnet test -- --filter-class EventPostsJobTests

## Backend API tests

* Task 22: Add OrganizationController tests for budget alert settings
    * Test enabling budget alerts with thresholds.
    * Test disabling budget alerts.
    * Test threshold normalization/deduplication.
    * Test invalid thresholds reject.
    * Test unauthorized organization update remains rejected.
    * Verification:
        * dotnet test -- --filter-class OrganizationControllerTests
* Task 23: Add ProjectController tests for event budget update behavior
    * Test setting fixed event budget.
    * Test setting percentage event budget.
    * Test clearing event budget.
    * Test invalid fixed budget rejects.
    * Test invalid percentage rejects.
    * Test percentage budget rejects for unlimited organization or is marked inactive according to final implementation decision.
    * Verification:
        * dotnet test -- --filter-class ProjectControllerTests
* Task 24: Add event submission integration tests
    * Verify event submission processing accepts samples under smart throttling.
    * Verify organization overage behavior remains unchanged.
    * Verify uncapped projects continue under the organization limit.
    * Verify crossing budget alert thresholds does not change accepted event response.
    * Verification:
        * dotnet test -- --filter-class EventControllerTests
        * dotnet test -- --filter-class EventPostsJobTests
* Task 25: Update HTTP samples if contracts are changed
    * Update tests/http/*.http samples for organization update/get if organization request/response examples exist.
    * Update tests/http/*.http samples for project update/get if project request/response examples exist.
    * Verification:
        * Manual review of tests/http/*.http.
        * dotnet build

## Svelte UI

* Task 26: Add budget alerts UI to Organization Usage page
    * Update src/Exceptionless.Web/ClientApp/src/routes/(app)/organization/[organizationId]/usage/+page.svelte.
    * Add card for enabling/disabling budget alerts.
    * Add threshold editor with default suggestions of 50 and 80.
    * Show computed event counts for each threshold.
    * Disable percentage alerts for unlimited organizations.
    * Use existing organization API query/mutation patterns.
    * Verification:
        * cd src/Exceptionless.Web/ClientApp && npm run check
        * cd src/Exceptionless.Web/ClientApp && npm run lint
        * Manual localhost QA on Organization Settings → Usage.
* Task 27: Add project event budget UI component
    * Create src/Exceptionless.Web/ClientApp/src/lib/features/projects/components/project-ingest-limit-card.svelte.
    * Support Off, Fixed, and Percentage modes.
    * Show computed effective cap.
    * Show current project usage against cap when active.
    * Show fixed-cap warning when fixed cap exceeds current organization allowance.
    * Disable percentage mode when organization allowance is unlimited.
    * Verification:
        * cd src/Exceptionless.Web/ClientApp && npm ci && npm run check
        * cd src/Exceptionless.Web/ClientApp && npm run lint
* Task 28: Add smart throttling UI status
    * Surface project smart-throttled status on Project Usage where feasible.
    * Explain that a sample of events is still accepted.
    * Link to organization/project usage.
    * Avoid exposing many tuning options.
    * Verification:
        * cd src/Exceptionless.Web/ClientApp && npm run check
        * cd src/Exceptionless.Web/ClientApp && npm run lint
        * Manual localhost QA with a smart-throttled project.
* Task 29: Integrate project card into Project Usage page
    * Add the card to src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/usage/+page.svelte.
    * Use existing getProjectQuery, getOrganizationQuery, and updateProject mutation.
    * Invalidate/update project query data after saving.
    * Verification:
        * cd src/Exceptionless.Web/ClientApp && npm run check
        * Manual localhost QA on /project/{projectId}/usage
* Task 30: Update project usage chart limit/status display
    * Use effective_ingest_limit as the chart limit when present.
    * Otherwise preserve existing organization limit behavior.
    * Label the limit line as Project Limit or Organization Limit.
    * Show smart throttling status text/badge when active.
    * Verification:
        * Manual localhost QA with:
            * no project budget
            * fixed project budget
            * percentage project budget
            * smart-throttled project
        * Confirm chart remains readable and accessible.
* Task 31: Add frontend tests for budget control helpers
    * Test organization budget alert threshold normalization.
    * Test organization budget alert threshold validation.
    * Test budget alert computed event counts.
    * Test project budget mode-to-payload conversion.
    * Test project percentage cap calculation display.
    * Test fixed cap warning.
    * Verification:
        * cd src/Exceptionless.Web/ClientApp && npm run test:unit

## Compatibility and validation

* Task 32: Verify existing request throttling remains unchanged
    * Confirm ThrottlingMiddleware behavior and tests are unchanged unless incidental generated code changes require updates.
    * Verification:
        * dotnet test -- --filter-class ThrottlingMiddlewareTests
* Task 33: Verify existing overage notification remains unchanged
    * Confirm existing PlanOverage monthly/hourly paths continue to enqueue and send existing organization notices.
    * Confirm budget alerts and smart throttling use separate message/work item/template.
    * Verification:
        * Existing organization notification tests if present.
        * New budget alert and smart throttling work item tests.
* Task 34: Verify no Elasticsearch mapping/reindex is required
    * Confirm budget alert settings and project event budget are not used for organization/project search/filter/sort.
    * Confirm no organization/project index version bump is required.
    * Verification:
        * Code review checklist item.
* Task 35: Local dogfood budget alerts
    * Run local stack with aspire run or Exceptionless.AppHost.
    * Use local QA URL only: http://localhost:7110.
    * Enable budget alerts for a low threshold.
    * Submit events until threshold is crossed.
    * Confirm budget alert work item/email is queued.
    * Confirm alert does not send twice for same threshold in same monthly period.
    * Confirm disabling alerts prevents future sends.
    * Verification:
        * Manual QA notes in PR.
* Task 36: Local dogfood project budgets and smart throttling
    * Run local stack with aspire run or Exceptionless.AppHost.
    * Use local QA URL only: http://localhost:7110.
    * Configure a low fixed budget on a project.
    * Submit events until budget is reached.
    * Confirm:
        * project is limited/sampled
        * blocked usage increases
        * another project can still ingest events
        * clearing the budget restores behavior
    * Create or simulate a noisy project.
    * Confirm smart throttling applies without manual configuration.
    * Confirm a sample is still accepted.
    * Verification:
        * Manual QA notes in PR.

## Final validation

* Task 37: Run targeted validation
    * Commands:
        * dotnet build
        * dotnet test -- --filter-class UsageServiceTests
        * dotnet test -- --filter-class OverageMiddlewareTests
        * dotnet test -- --filter-class OrganizationControllerTests
        * dotnet test -- --filter-class ProjectControllerTests
        * dotnet test -- --filter-class OrganizationBudgetAlertWorkItemHandlerTests
        * dotnet test -- --filter-class ProjectSmartThrottleNotificationWorkItemHandlerTests
        * dotnet test -- --filter-class EventControllerTests
        * dotnet test -- --filter-class EventPostsJobTests
        * dotnet test -- --filter-class ThrottlingMiddlewareTests
        * cd src/Exceptionless.Web/ClientApp && npm ci && npm run check
        * cd src/Exceptionless.Web/ClientApp && npm run lint
        * cd src/Exceptionless.Web/ClientApp && npm run test:unit
* Task 38: Run OpenSpec validation
    * Command:
        * openspec validate add-usage-budget-controls --strict --no-interactive
* Task 39: Optional full validation before merge
    * Commands:
        * dotnet test
        * cd src/Exceptionless.Web/ClientApp && npm run build
        * openspec validate --all --strict --no-interactive
