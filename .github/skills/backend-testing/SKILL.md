---
name: Backend Testing
description: |
  Backend testing with xUnit, Foundatio.Xunit, integration tests with AppWebHostFactory,
  FluentClient, ProxyTimeProvider for time manipulation, and test data builders.
  Keywords: xUnit, Fact, Theory, integration tests, AppWebHostFactory, FluentClient,
  ProxyTimeProvider, TimeProvider, Foundatio.Xunit, TestWithLoggingBase, test data builders
---

# Backend Testing

## Test Naming Standards

Follow Microsoft's [unit testing best practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices#follow-test-naming-standards):

**Pattern**: `MethodUnderTest_Scenario_ExpectedBehavior`

- **MethodUnderTest** — The actual method on the class being tested, not necessarily the entry point you call. For example, when testing `ObjectToInferredTypesConverter`, use `Read` (the converter's method) even though you invoke it via `_serializer.Deserialize()`.
- **Scenario** — The input, state, or condition being tested.
- **ExpectedBehavior** — What the method should do or return.

```csharp
// ✅ Good: Clear method, scenario, and expected behavior
[Fact]
public void GetValue_JObjectWithUserInfo_ReturnsTypedUserInfo() { }

[Fact]
public void GetValue_MissingKey_ThrowsKeyNotFoundException() { }

[Fact]
public void Read_EmptyArray_ReturnsEmptyList() { }  // Tests ObjectToInferredTypesConverter.Read()

[Fact]
public void Write_NestedDictionary_SerializesCorrectly() { }  // Tests ObjectToInferredTypesConverter.Write()

[Fact]
public async Task PostEvent_WithValidPayload_ReturnsAccepted() { }

// ❌ Bad: Vague or missing context
[Fact]
public void TestGetValue() { }

[Fact]
public void CanGetValue() { }

[Fact]
public void Deserialize_EmptyArray_ReturnsEmptyList() { }  // Wrong: Deserialize is the entry point, not the method under test
```

## Running Tests

```bash
# All tests
dotnet test

# By test name
dotnet test --filter "FullyQualifiedName~PostEvent_WithValidPayload_ReturnsAccepted"

# By class name
dotnet test --filter "ClassName~EventControllerTests"
```

## Test Folder Structure

Tests mirror the source structure:

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
│   ├── EventData.cs
│   ├── OrganizationData.cs
│   ├── ProjectData.cs
│   ├── ProxyTimeProvider.cs     # Time manipulation
│   └── ...
└── Validation/                  # Validator tests
```

## Integration Test Base Pattern

Inherit from `IntegrationTestsBase` which uses Foundatio.Xunit's `TestWithLoggingBase`:

```csharp
// From tests/Exceptionless.Tests/IntegrationTestsBase.cs
public abstract class IntegrationTestsBase : TestWithLoggingBase, IAsyncLifetime, IClassFixture<AppWebHostFactory>
{
    protected readonly TestServer _server;
    private readonly ProxyTimeProvider _timeProvider;

    public IntegrationTestsBase(ITestOutputHelper output, AppWebHostFactory factory) : base(output)
    {
        _server = factory.Server;
        _timeProvider = GetService<ProxyTimeProvider>();
    }

    protected TService GetService<TService>() where TService : notnull
        => ServiceProvider.GetRequiredService<TService>();

    protected FluentClient CreateFluentClient()
    {
        var settings = GetService<JsonSerializerSettings>();
        return new FluentClient(CreateHttpClient(), new NewtonsoftJsonSerializer(settings));
    }
}
```

## Real Test Example

From [EventControllerTests.cs](tests/Exceptionless.Tests/Controllers/EventControllerTests.cs):

```csharp
public class EventControllerTests : IntegrationTestsBase
{
    private readonly IEventRepository _eventRepository;
    private readonly IQueue<EventPost> _eventQueue;

    public EventControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _eventRepository = GetService<IEventRepository>();
        _eventQueue = GetService<IQueue<EventPost>>();
    }

    [Fact]
    public async Task PostEvent_WithValidPayload_EnqueuesAndProcessesEvent()
    {
        // Arrange
        /* language=json */
        const string json = """{"message":"test","reference_id":"TestReferenceId"}""";

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationClientUser()
            .AppendPath("events")
            .Content(json, "application/json")
            .StatusCodeShouldBeAccepted()
        );

        var stats = await _eventQueue.GetQueueStatsAsync();
        Assert.Equal(1, stats.Enqueued);

        var processEventsJob = GetService<EventPostsJob>();
        await processEventsJob.RunAsync();
        await RefreshDataAsync();

        // Assert
        var events = await _eventRepository.GetAllAsync();
        var ev = events.Documents.Single(e => e.Type == Event.KnownTypes.Log);
        Assert.Equal("test", ev.Message);
    }
}
```

## Test Structure (Arrange-Act-Assert)

Use clear `// Arrange`, `// Act`, `// Assert` comments for readability:

```csharp
[Fact]
public void GetValue_DirectUserInfoType_ReturnsTypedValue()
{
    // Arrange
    var userInfo = new UserInfo("test@example.com", "Test User");
    var data = new DataDictionary { { "user", userInfo } };

    // Act
    var result = data.GetValue<UserInfo>("user", _jsonOptions);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("test@example.com", result.Identity);
    Assert.Equal("Test User", result.Name);
}
```

## JSON String Literals

Use `/* language=json */` comment before JSON strings for IDE syntax highlighting and validation:

```csharp
[Fact]
public void GetValue_JsonStringWithError_ReturnsTypedError()
{
    // Arrange
    /* language=json */
    const string json = """{"message":"Test error","type":"System.Exception"}""";
    var data = new DataDictionary { { "@error", json } };

    // Act
    var result = data.GetValue<Error>("@error", _jsonOptions);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("Test error", result.Message);
}
```

## FluentClient Pattern

Use `SendRequestAsync` with `AppSendBuilder` for HTTP testing:

```csharp
await SendRequestAsync(r => r
    .Post()
    .AsTestOrganizationUser()          // Basic auth with test user
    .AppendPath("organizations")
    .Content(new NewOrganization { Name = "Test" })
    .StatusCodeShouldBeCreated()
);

// Available auth helpers
r.AsGlobalAdminUser()          // TEST_USER_EMAIL
r.AsTestOrganizationUser()     // TEST_ORG_USER_EMAIL
r.AsFreeOrganizationUser()     // FREE_USER_EMAIL
r.AsTestOrganizationClientUser() // API key bearer token
```

## Test Data Builders

Create test data with `CreateDataAsync`:

```csharp
var (stacks, events) = await CreateDataAsync(b => b
    .Event()
    .TestProject()
    .Type(Event.KnownTypes.Error)
    .Message("Test error"));

Assert.Single(stacks);
Assert.Single(events);
```

## Time Manipulation with ProxyTimeProvider

**NOT `ISystemClock`** — use .NET 8+ `TimeProvider` with `ProxyTimeProvider`:

```csharp
// Access via protected property
protected ProxyTimeProvider TimeProvider => _timeProvider;

// Advance time
TimeProvider.Advance(TimeSpan.FromHours(1));

// Set specific time
TimeProvider.SetUtcNow(new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero));

// Restore to system time
TimeProvider.Restore();
```

Registered in test services:

```csharp
services.ReplaceSingleton<TimeProvider>(_ => new ProxyTimeProvider());
```

## Test Principles

- **TDD workflow** — When fixing bugs or adding features, write a failing test first
- **Use real serializer** — Tests use the same JSON serializer as production
- **Use real time provider** — Manipulate via `ProxyTimeProvider` when needed
- **Refresh after writes** — Call `RefreshDataAsync()` after database changes
- **Clean state** — `ResetDataAsync()` clears data between tests

## Foundatio.Xunit

Base class provides logging integration:

```csharp
using Foundatio.Xunit;

public class MyTests : TestWithLoggingBase
{
    public MyTests(ITestOutputHelper output) : base(output)
    {
        Log.DefaultLogLevel = LogLevel.Information;
    }
}
```
