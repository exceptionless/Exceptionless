---
name: backend-testing
description: >
    Use this skill when writing or modifying C# tests — unit tests, integration tests, or
    test fixtures. Covers xUnit patterns, AppWebHostFactory for integration testing, FluentClient
    for API assertions, ProxyTimeProvider for time manipulation, and test data builders. Apply
    when adding new test cases, debugging test failures, or setting up test infrastructure.
---

# Backend Testing

## Test Naming Standards

**Pattern**: `MethodUnderTest_Scenario_ExpectedBehavior`

- **MethodUnderTest** — The actual method on the class being tested, not necessarily the entry point you call.
- **Scenario** — The input, state, or condition being tested.
- **ExpectedBehavior** — What the method should do or return.

```csharp
// ✅ Good
[Fact]
public void GetValue_JObjectWithUserInfo_ReturnsTypedUserInfo() { }
[Fact]
public async Task PostEvent_WithValidPayload_ReturnsAccepted() { }

// ❌ Bad: Vague or wrong method name
[Fact]
public void TestGetValue() { }
[Fact]
public void Deserialize_EmptyArray_ReturnsEmptyList() { }  // Wrong: name the method under test, not the entry point
```

## Test Folder Structure

```text
tests/Exceptionless.Tests/
├── AppWebHostFactory.cs         # WebApplicationFactory for integration tests
├── IntegrationTestsBase.cs      # Base class for integration tests
├── TestWithServices.cs          # Base class for unit tests with DI
├── Api/                         # Minimal API tests, organized by production layer
│   ├── Endpoints/               # HTTP integration tests by endpoint family
│   ├── Filters/                 # Endpoint filter unit tests
│   ├── Handlers/                # Mediator handler unit tests
│   └── Results/                 # API result mapping tests
├── Jobs/                        # Job tests
├── Repositories/                # Repository tests
├── Services/                    # Service tests
├── Utility/                     # Test data builders
│   ├── AppSendBuilder.cs        # Fluent HTTP request builder
│   ├── DataBuilder.cs           # Test data creation
│   ├── ProxyTimeProvider.cs     # Time manipulation
│   └── ...
└── Validation/                  # Validator tests
```

## Integration Test Base

Inherit from `IntegrationTestsBase` (extends Foundatio.Xunit's `TestWithLoggingBase`):

```csharp
public abstract class IntegrationTestsBase : TestWithLoggingBase, IAsyncLifetime, IClassFixture<AppWebHostFactory>
```

Key members: `GetService<T>()`, `CreateFluentClient()`, `SendRequestAsync()`, `RefreshDataAsync()`, `ResetDataAsync()`, `TimeProvider` (ProxyTimeProvider).

## HTTP Test Pattern

Use `SendRequestAsync` with `AppSendBuilder` for HTTP testing. Always configure the expected response status with `StatusCodeShouldBe...`; the helper validates it before returning the response. Preserve that expectation when restructuring a test into Arrange/Act/Assert sections:

```csharp
await SendRequestAsync(r => r
    .Post()
    .AsTestOrganizationUser()
    .AppendPath("organizations")
    .Content(new NewOrganization { Name = "Test" })
    .StatusCodeShouldBeCreated()
);
```

Auth helpers: `AsGlobalAdminUser()`, `AsTestOrganizationUser()`, `AsFreeOrganizationUser()`, `AsTestOrganizationClientUser()` (API key bearer token).

## Test Data Builders

```csharp
var (stacks, events) = await CreateDataAsync(b => b
    .Event()
    .TestProject()
    .Type(Event.KnownTypes.Error)
    .Message("Test error"));
```

## ProxyTimeProvider

**NOT `ISystemClock`** — use .NET 8+ `TimeProvider` with `ProxyTimeProvider`:

```csharp
TimeProvider.Advance(TimeSpan.FromHours(1));
TimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero));
TimeProvider.Restore();
```

## Test Structure

- Every new or modified test must explicitly label `// Arrange`, `// Act`, and `// Assert` in that order. Check every changed test before finishing a review.
- Arrange inputs, mocks, and prerequisites; Act runs the behavior under test; Assert checks the result. Keep behavior-changing calls out of Assert.
- If setup comes entirely from a fixture or theory data, identify that in the Arrange comment. For multi-step scenarios, repeat labeled Act/Assert sections at each transition.
- Keep fluent HTTP assertions and exception assertions in the existing project style; label a combined `// Act & Assert` section when execution and verification are inseparable. Do not add helper abstractions or redundant setup just to create sections.

## Test Principles

- **Regression coverage** — Add a focused failing test first when a bug fix can be reproduced cheaply
- **Use real serializer** — Tests use the same JSON serializer as production
- **Refresh after writes** — Call `RefreshDataAsync()` after database changes
- **Clean state** — `ResetDataAsync()` clears data between integration tests
