# Risk Register: Minimal API + Mediator + OpenAPI Migration

## Risk 1: Route Regression

| Field | Value |
|-------|-------|
| Likelihood | Medium |
| Impact | High |
| Description | A route path, HTTP method, or parameter binding is accidentally changed during migration, breaking SDK/client compatibility. |
| Mitigation | Route manifest snapshot tests detect any path/method change. OpenAPI snapshot tests detect parameter/response drift. Both run in CI. |
| Detection | CI fails on snapshot mismatch. |

## Risk 2: Auth Bypass

| Field | Value |
|-------|-------|
| Likelihood | Low |
| Impact | Critical |
| Description | An endpoint is migrated without the correct authorization policy, allowing unauthenticated/unauthorized access. |
| Mitigation | Auth policies applied at group level via `ApiEndpointGroups.cs`. Per-endpoint overrides (AllowAnonymous, GlobalAdmin) explicitly mapped. Existing auth integration tests cover all protected endpoints. |
| Detection | Existing integration tests fail. Manual review of endpoint registration. |

## Risk 3: Validation Gaps

| Field | Value |
|-------|-------|
| Likelihood | Medium |
| Impact | Medium |
| Description | Minimal API automatic validation does not trigger for a DTO, or a JSON Patch/legacy partial body bypasses operation or merged-model validation. |
| Mitigation | Validation tests verify error shapes, patch operations, legacy-body conversion, and merged-model validation before persistence. |
| Detection | Validation tests fail. Manual review during PR. |

## Risk 4: OpenAPI Drift

| Field | Value |
|-------|-------|
| Likelihood | Medium |
| Impact | Medium |
| Description | The generated OpenAPI document differs from the baseline (different operation IDs, missing parameters, changed schemas) breaking documentation or code generators. |
| Mitigation | OpenAPI snapshot test compares against baseline. Build-time artifact generation ensures reproducibility. |
| Detection | Snapshot test fails in CI. |

## Risk 5: Middleware Ordering

| Field | Value |
|-------|-------|
| Likelihood | Low |
| Impact | High |
| Description | Pipeline ordering changes cause throttling/overage middleware to not execute, or execute in wrong order relative to auth. |
| Mitigation | Middleware registration order preserved in Program.cs. No middleware implementations changed. Integration tests exercise full pipeline. |
| Detection | Rate limiting / overage tests fail. Manual pipeline audit in Task 19. |

## Risk 6: Breaking Changes in Response Headers

| Field | Value |
|-------|-------|
| Likelihood | Medium |
| Impact | Medium |
| Description | Custom response headers (pagination links, configuration version, rate-limit) are lost when moving from action filters to endpoint filters. |
| Mitigation | Endpoint filters (`ApiResponseHeadersEndpointFilter`, `ConfigurationResponseEndpointFilter`) replicate existing action filter behavior. Integration tests verify headers. |
| Detection | Tests checking response headers fail. |

## Risk 7: Rollback Complexity

| Field | Value |
|-------|-------|
| Likelihood | Low |
| Impact | Medium |
| Description | If a late-stage migration (e.g., EventController) causes issues, rolling back requires re-adding the controller while the Api infrastructure is already in place. |
| Mitigation | Each controller migration is a separate, independently revertible PR. The Api infrastructure (Task 2-4) is additive and does not conflict with controllers. Reverting a controller migration PR restores the controller without affecting other migrated endpoints. |
| Detection | Git revert + CI green confirms clean rollback. |

## Risk 8: Raw Event Ingestion Regression

| Field | Value |
|-------|-------|
| Likelihood | Medium |
| Impact | High |
| Description | Event ingestion (multipart, compressed, raw body) has complex model binding that may not translate directly to Minimal API parameter binding. |
| Mitigation | EventEndpoints is migrated last (Task 17) after all simpler endpoints validate the pattern. Dedicated event ingestion tests verify all content types. Manual smoke test with real SDK submission. |
| Detection | Event ingestion integration tests fail. Manual smoke test in Task 19. |

## Risk 9: Foundatio.Mediator Version Compatibility

| Field | Value |
|-------|-------|
| Likelihood | Low |
| Impact | Low |
| Description | Foundatio.Mediator API may change or have undocumented behavior that affects handler dispatch. |
| Mitigation | Exceptionless already depends on Foundatio packages. Mediator registration smoke test (Task 3) validates DI and dispatch work before any endpoint migration begins. |
| Detection | Smoke test fails in Task 3. |
