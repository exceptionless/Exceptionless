# Tasks: Minimal API + Mediator + OpenAPI Migration

## Task 1: Contract/OpenAPI Baseline Tests

**Goal**: Establish snapshot baselines before any migration work begins.

**Work**:
- Add a test that starts the web host and captures the full OpenAPI document as a snapshot.
- Add a test that enumerates all registered routes (method + path) and captures as a route manifest snapshot.
- Check in baseline snapshot files.

**Verification**:
```bash
dotnet test --filter "FullyQualifiedName~OpenApiSnapshotTests"
dotnet test --filter "FullyQualifiedName~RouteManifestTests"
```

---

## Task 2: Api Infrastructure

**Goal**: Create the folder structure and shared utilities that all endpoints depend on.

**Work**:
- Create `src/Exceptionless.Web/Api/` folder structure.
- Implement `ApiEndpoints.cs` (empty, calls no feature endpoints yet).
- Implement `ApiEndpointGroups.cs` (shared group builder with prefix, auth, OpenAPI).
- Implement `Results/ApiResults.cs`, `Results/OkWithHeadersResult.cs`, `Results/CollectionResult.cs`.
- Implement `Infrastructure/Pagination.cs`, `Infrastructure/TimeRangeParser.cs`, `Infrastructure/CurrentUserAccessor.cs`.
- Implement `Filters/ApiResponseHeadersEndpointFilter.cs`, `Filters/ConfigurationResponseEndpointFilter.cs`.
- Implement `Infrastructure/ApiProblemDetails.cs` (ProblemDetails customization).
- Implement `Infrastructure/ApiValidation.cs` (MiniValidation helper).

**Verification**:
```bash
dotnet build src/Exceptionless.Web/Exceptionless.Web.csproj
```

---

## Task 3: Mediator Registration

**Goal**: Configure Foundatio.Mediator DI so handlers are auto-discovered.

**Work**:
- Add Foundatio.Mediator registration in DI (Bootstrapper or Program.cs).
- Register handler assemblies for auto-discovery.
- Add a smoke test that resolves IMediator from DI and dispatches a no-op message.

**Verification**:
```bash
dotnet build src/Exceptionless.Web/Exceptionless.Web.csproj
dotnet test --filter "FullyQualifiedName~MediatorRegistrationTests"
```

---

## Task 4: Validation and ProblemDetails Integration

**Goal**: Wire up automatic validation for Minimal API DTOs and ProblemDetails customization.

**Work**:
- Configure `AddProblemDetails()` with instance, reference-id, lower_underscore error keys.
- Add `Middleware/ValidationMiddleware.cs` (endpoint filter for automatic DTO validation).
- Verify patch validation works for RFC 6902 documents and legacy partial object bodies.
- Add tests for validation error response shape.

**Verification**:
```bash
dotnet test --filter "FullyQualifiedName~ValidationProblemDetailsTests"
```

---

## Task 5: StatusEndpoints

**Goal**: Migrate `StatusController` to Minimal API.

**Work**:
- Create `Endpoints/StatusEndpoints.cs` with all routes from StatusController.
- Create `Messages/StatusMessages.cs` (GetAbout, GetQueueStats, PostReleaseNotification, Get/Post/DeleteSystemNotification).
- Create `Handlers/StatusHandler.cs`.
- Wire into `ApiEndpoints.cs`.
- Remove `Controllers/StatusController.cs`.

**Verification**:
```bash
dotnet test
# Manual: curl http://localhost:7110/api/v2/about
```

---

## Task 6: UtilityEndpoints

**Goal**: Migrate `UtilityController` to Minimal API.

**Work**:
- Create `Endpoints/UtilityEndpoints.cs`.
- Create messages and handler if needed (may be thin enough to inline).
- Wire into `ApiEndpoints.cs`.
- Remove `Controllers/UtilityController.cs`.

**Verification**:
```bash
dotnet test
dotnet test --filter "FullyQualifiedName~RouteManifestTests"
```

---

## Task 7: TokenEndpoints

**Goal**: Migrate `TokenController` to Minimal API.

**Work**:
- Create `Endpoints/TokenEndpoints.cs`, `Messages/TokenMessages.cs`, `Handlers/TokenHandler.cs`.
- Include v1 aliases if any exist.
- Wire into `ApiEndpoints.cs`.
- Remove `Controllers/TokenController.cs`.

**Verification**:
```bash
dotnet test
dotnet test --filter "FullyQualifiedName~OpenApiSnapshotTests"
```

---

## Task 8: SavedViewEndpoints

**Goal**: Migrate `SavedViewController` to Minimal API.

**Work**:
- Create `Endpoints/SavedViewEndpoints.cs`, `Messages/SavedViewMessages.cs`, `Handlers/SavedViewHandler.cs`.
- Wire into `ApiEndpoints.cs`.
- Remove `Controllers/SavedViewController.cs`.

**Verification**:
```bash
dotnet test
```

---

## Task 9: ProjectEndpoints

**Goal**: Migrate `ProjectController` to Minimal API.

**Work**:
- Create `Endpoints/ProjectEndpoints.cs`, `Messages/ProjectMessages.cs`, `Handlers/ProjectHandler.cs`.
- Include config endpoint, notification settings, integration endpoints.
- Include v1 aliases.
- Wire into `ApiEndpoints.cs`.
- Remove `Controllers/ProjectController.cs`.

**Verification**:
```bash
dotnet test
dotnet test --filter "FullyQualifiedName~RouteManifestTests"
dotnet test --filter "FullyQualifiedName~OpenApiSnapshotTests"
```

---

## Task 10: OrganizationEndpoints

**Goal**: Migrate `OrganizationController` to Minimal API.

**Work**:
- Create `Endpoints/OrganizationEndpoints.cs`, `Messages/OrganizationMessages.cs`, `Handlers/OrganizationHandler.cs`.
- Include invoice, plan, suspend, billing endpoints.
- Wire into `ApiEndpoints.cs`.
- Remove `Controllers/OrganizationController.cs`.

**Verification**:
```bash
dotnet test
```

---

## Task 11: StackEndpoints

**Goal**: Migrate `StackController` to Minimal API.

**Work**:
- Create `Endpoints/StackEndpoints.cs`, `Messages/StackMessages.cs`, `Handlers/StackHandler.cs`.
- Include mark-fixed, mark-critical, snooze, promote endpoints.
- Wire into `ApiEndpoints.cs`.
- Remove `Controllers/StackController.cs`.

**Verification**:
```bash
dotnet test
```

---

## Task 12: UserEndpoints

**Goal**: Migrate `UserController` to Minimal API.

**Work**:
- Create `Endpoints/UserEndpoints.cs`, `Messages/UserMessages.cs`, `Handlers/UserHandler.cs`.
- Include email verification, admin email endpoints.
- Wire into `ApiEndpoints.cs`.
- Remove `Controllers/UserController.cs`.

**Verification**:
```bash
dotnet test
```

---

## Task 13: WebHookEndpoints

**Goal**: Migrate `WebHookController` to Minimal API.

**Work**:
- Create `Endpoints/WebHookEndpoints.cs`, `Messages/WebHookMessages.cs`, `Handlers/WebHookHandler.cs`.
- Wire into `ApiEndpoints.cs`.
- Remove `Controllers/WebHookController.cs`.

**Verification**:
```bash
dotnet test
```

---

## Task 14: StripeEndpoints

**Goal**: Migrate `StripeController` to Minimal API.

**Work**:
- Create `Endpoints/StripeEndpoints.cs`.
- Stripe webhook handler may not need mediator (direct processing).
- Wire into `ApiEndpoints.cs`.
- Remove `Controllers/StripeController.cs`.

**Verification**:
```bash
dotnet test
```

---

## Task 15: AuthEndpoints

**Goal**: Migrate `AuthController` to Minimal API.

**Work**:
- Create `Endpoints/AuthEndpoints.cs`, `Messages/AuthMessages.cs`, `Handlers/AuthHandler.cs`.
- Include login, signup, OAuth callbacks, forgot-password, change-password.
- Preserve all auth/authorization behavior exactly.
- Wire into `ApiEndpoints.cs`.
- Remove `Controllers/AuthController.cs`.

**Verification**:
```bash
dotnet test
# Manual: verify login flow at http://localhost:7110
```

---

## Task 16: AdminEndpoints

**Goal**: Migrate `AdminController` to Minimal API.

**Work**:
- Create `Endpoints/AdminEndpoints.cs`, `Messages/AdminMessages.cs`, `Handlers/AdminHandler.cs`.
- Preserve GlobalAdmin policy.
- Wire into `ApiEndpoints.cs`.
- Remove `Controllers/AdminController.cs`.

**Verification**:
```bash
dotnet test
```

---

## Task 17: EventEndpoints

**Goal**: Migrate `EventController` to Minimal API (most complex).

**Work**:
- Create `Endpoints/EventEndpoints.cs`, `Messages/EventMessages.cs`, `Handlers/EventHandler.cs`.
- Preserve raw event ingestion (multipart, compressed, raw body).
- Preserve query/count/session endpoints.
- Preserve v1 aliases.
- Wire into `ApiEndpoints.cs`.
- Remove `Controllers/EventController.cs`.

**Verification**:
```bash
dotnet test
dotnet test --filter "FullyQualifiedName~EventIngestion"
dotnet test --filter "FullyQualifiedName~RouteManifestTests"
dotnet test --filter "FullyQualifiedName~OpenApiSnapshotTests"
```

---

## Task 18: Remove Controllers Infrastructure

**Goal**: Remove MVC controller infrastructure.

**Work**:
- Remove `AddControllers()` from DI registration.
- Remove `MapControllers()` from endpoint mapping.
- Delete `Controllers/` folder and `Controllers/Base/` folder.
- Remove any MVC-specific action filters that are fully replaced by endpoint filters.
- Verify build succeeds without MVC controller support.

**Verification**:
```bash
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~RouteManifestTests"
dotnet test --filter "FullyQualifiedName~OpenApiSnapshotTests"
```

---

## Task 19: Final OpenAPI/Route/Middleware Audit

**Goal**: Final verification that the migration is complete and correct.

**Work**:
- Update route manifest snapshot (should match pre-migration baseline).
- Update OpenAPI snapshot (should match pre-migration baseline modulo non-breaking metadata).
- Verify middleware pipeline order matches pre-migration.
- Verify `tests/http/*.http` files work against new endpoints.
- Verify PATCH endpoints accept both RFC 6902 and legacy partial object request bodies without changing partial-update semantics.
- Run full test suite.
- Manual smoke test of login, event submission, and dashboard at http://localhost:7110.

**Verification**:
```bash
dotnet build
dotnet test
# Manual smoke test:
# curl http://localhost:7110/api/v2/about
# Login as admin@exceptionless.test / tester
# Submit test event and verify it appears
```
