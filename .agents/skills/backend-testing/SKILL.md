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
├── Controllers/                 # API controller tests
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

## FluentClient Pattern

Use `SendRequestAsync` with `AppSendBuilder` for HTTP testing:

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

## Test Principles

- **TDD workflow** — Write a failing test first when fixing bugs or adding features
- **Use real serializer** — Tests use the same JSON serializer as production
- **Refresh after writes** — Call `RefreshDataAsync()` after database changes
- **Clean state** — `ResetDataAsync()` clears data between tests
- **AAA comments** — Use `// Arrange`, `// Act`, `// Assert` for readability
