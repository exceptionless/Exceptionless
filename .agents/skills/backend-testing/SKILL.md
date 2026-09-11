---
name: backend-testing
description: Write Exceptionless C# tests using local fixtures, HTTP helpers, and controlled time.
---

# Backend Testing

## Unit test structure

Follow [Microsoft's .NET unit testing best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices).

Organize tests as **Arrange–Act–Assert**:

- **Arrange:** create the subject, minimal input, and required dependencies.
- **Act:** perform the behavior under test, awaiting asynchronous work.
- **Assert:** check the observable result and relevant side effects. Keep assertions separate from the action.

Keep unit tests fast, isolated, repeatable, and self-checking. Use clear names and minimal setup; test behavior rather than implementation details. Avoid branching or recreating production logic to calculate expected results. Prefer separate cases or parameterized tests for different scenarios. Infrastructure-dependent checks belong in integration tests, using the fixtures below. Coverage percentages alone do not establish test quality.

## Fixtures and locations

Under `tests/Exceptionless.Tests/`:

- `IntegrationTestsBase.cs` and `AppWebHostFactory.cs`: HTTP/service integration fixtures.
- `TestWithServices.cs`: tests needing dependency injection.
- `Api/Endpoints/`, `Api/Filters/`, `Api/Handlers/`, and `Api/Results/`: API tests by boundary.
- `Utility/AppSendBuilder.cs`: fluent HTTP requests and assertions.
- `Utility/DataBuilder.cs`: synthetic records.
- `Utility/ProxyTimeProvider.cs`: controlled time.

Name tests `MethodUnderTest_Scenario_ExpectedBehavior`.

## HTTP and state

Use `SendRequestAsync` with `AppSendBuilder` and its authorization helpers: `AsGlobalAdminUser`, `AsTestOrganizationUser`, `AsFreeOrganizationUser`, and `AsTestOrganizationClientUser`. Match an existing endpoint test for the expected response and fixture setup.

Use `CreateDataAsync` for synthetic data and `RefreshDataAsync` after database writes when reads require index refresh. `ResetDataAsync` clears the integration fixture's data; never point these fixtures at production.

Tests use the production serializer. For JSON compatibility, inspect `Serializer/` and the relevant API snapshots.

## Time

Use the fixture's `TimeProvider`: `Advance`, `SetUtcNow`, and `Restore`. Prefer controlled time to real waits.

Follow root guidance for focused commands, conditional Aspire startup, and API contract verification.
