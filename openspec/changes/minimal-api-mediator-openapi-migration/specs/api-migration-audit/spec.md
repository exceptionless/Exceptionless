## ADDED Requirements

### Requirement: Minimal API migration audit evidence

The migration SHALL include automated evidence that public API routes, OpenAPI output, validation behavior, middleware behavior, and representative local endpoint performance are reviewed before merge.

#### Scenario: Public route manifest remains compatible

Given the Minimal API endpoint layer replaces MVC controllers
When the controller route manifest from `main` is compared with the endpoint manifest on the migration branch
Then every public method and route from `main` is present on the migration branch
And the migration branch does not introduce unexpected public method and route pairs.

#### Scenario: Runtime endpoint performance is measured

Given the migration branch is expected to improve hot endpoint performance
When representative local endpoints are benchmarked against current `main`
Then the PR description records the measured median and p95 timings
And any slower endpoint is called out for follow-up before merge.

#### Scenario: Middleware preservation is verified

Given project configuration and throttling are middleware responsibilities
When middleware tests and endpoint benchmarks are run
Then the PR description records middleware coverage and project configuration timing
And existing middleware is preserved unless measured evidence supports replacing it.
