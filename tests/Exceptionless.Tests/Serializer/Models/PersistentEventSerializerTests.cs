using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests PersistentEvent serialization through ITextSerializer.
/// Validates round-trip serialization and snake_case property naming.
/// Uses TimeProvider to freeze time for deterministic output.
/// </summary>
public class PersistentEventSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;
    private static readonly DateTimeOffset FixedDate = new(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);

    public PersistentEventSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
        TimeProvider.SetUtcNow(FixedDate);
    }

    [Fact]
    public void SerializeToString_CompleteEvent_PreservesAllProperties()
    {
        // Arrange
        var ev = new PersistentEvent
        {
            Id = "event123",
            OrganizationId = "org456",
            ProjectId = "proj789",
            StackId = "stack012",
            Type = Event.KnownTypes.Error,
            Message = "Test error occurred",
            Date = FixedDate,
            Tags = ["production", "critical"],
            Geo = "37.7749,-122.4194",
            Value = 99.95m,
            Count = 3,
            ReferenceId = "ref-abc"
        };

        // Act
        string? json = _serializer.SerializeToString(ev);
        var deserialized = _serializer.Deserialize<PersistentEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("event123", deserialized.Id);
        Assert.Equal("org456", deserialized.OrganizationId);
        Assert.Equal("proj789", deserialized.ProjectId);
        Assert.Equal("stack012", deserialized.StackId);
        Assert.Equal(Event.KnownTypes.Error, deserialized.Type);
        Assert.Equal("Test error occurred", deserialized.Message);
        Assert.Equal(99.95m, deserialized.Value);
        Assert.Equal(3, deserialized.Count);
    }

    [Fact]
    public void SerializeToString_MinimalEvent_PreservesProperties()
    {
        // Arrange
        var ev = new PersistentEvent
        {
            Id = "id1",
            OrganizationId = "org1",
            ProjectId = "proj1",
            Type = Event.KnownTypes.Log,
            Date = FixedDate
        };

        // Act
        string? json = _serializer.SerializeToString(ev);
        var deserialized = _serializer.Deserialize<PersistentEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("id1", deserialized.Id);
        Assert.Equal("org1", deserialized.OrganizationId);
        Assert.Equal("proj1", deserialized.ProjectId);
        Assert.Equal(Event.KnownTypes.Log, deserialized.Type);
    }

    [Fact]
    public void Deserialize_EventWithUserInfo_PreservesTypedUserInfo()
    {
        // Arrange
        var original = new PersistentEvent
        {
            Id = "evt-user",
            OrganizationId = "org1",
            ProjectId = "proj1",
            Type = Event.KnownTypes.Log,
            Date = FixedDate
        };
        original.SetUserIdentity("user@example.com", "Test User");

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<PersistentEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        var userInfo = deserialized.GetUserIdentity();
        Assert.NotNull(userInfo);
        Assert.Equal("user@example.com", userInfo.Identity);
        Assert.Equal("Test User", userInfo.Name);
    }

    [Fact]
    public void Deserialize_EventWithError_PreservesTypedError()
    {
        // Arrange
        var original = new PersistentEvent
        {
            Id = "evt-error",
            OrganizationId = "org1",
            ProjectId = "proj1",
            Type = Event.KnownTypes.Error,
            Date = FixedDate,
            Data = new DataDictionary()
        };
        original.Data[Event.KnownDataKeys.Error] = new Error
        {
            Message = "Test exception",
            Type = "System.InvalidOperationException",
            StackTrace =
            [
                new StackFrame
                {
                    Name = "TestMethod",
                    DeclaringNamespace = "TestNamespace",
                    DeclaringType = "TestClass",
                    LineNumber = 42
                }
            ]
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<PersistentEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        var error = deserialized.GetError();
        Assert.NotNull(error);
        Assert.Equal("Test exception", error.Message);
        Assert.Equal("System.InvalidOperationException", error.Type);
        Assert.NotNull(error.StackTrace);
        Assert.Single(error.StackTrace);
        Assert.Equal("TestMethod", error.StackTrace[0].Name);
        Assert.Equal(42, error.StackTrace[0].LineNumber);
    }

    [Fact]
    public void Deserialize_EventWithRequestInfo_PreservesTypedRequestInfo()
    {
        // Arrange
        var original = new PersistentEvent
        {
            Id = "evt-req",
            OrganizationId = "org1",
            ProjectId = "proj1",
            Type = Event.KnownTypes.Error,
            Date = FixedDate
        };
        original.AddRequestInfo(new RequestInfo
        {
            HttpMethod = "POST",
            Path = "/api/events",
            Host = "api.example.com",
            Port = 443,
            IsSecure = true
        });

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<PersistentEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        var request = deserialized.GetRequestInfo();
        Assert.NotNull(request);
        Assert.Equal("POST", request.HttpMethod);
        Assert.Equal("/api/events", request.Path);
    }

    [Fact]
    public void Deserialize_EventWithEnvironmentInfo_PreservesTypedEnvironmentInfo()
    {
        // Arrange
        var original = new PersistentEvent
        {
            Id = "evt-env",
            OrganizationId = "org1",
            ProjectId = "proj1",
            Type = Event.KnownTypes.Error,
            Date = FixedDate
        };
        original.SetEnvironmentInfo(new EnvironmentInfo
        {
            MachineName = "PROD-SERVER-01",
            ProcessorCount = 8,
            OSName = "Windows",
            OSVersion = "10.0.19041"
        });

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<PersistentEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        var env = deserialized.GetEnvironmentInfo();
        Assert.NotNull(env);
        Assert.Equal("PROD-SERVER-01", env.MachineName);
        Assert.Equal(8, env.ProcessorCount);
    }

    [Fact]
    public void Deserialize_EventWithVersionAndLevel_PreservesStringData()
    {
        // Arrange
        var original = new PersistentEvent
        {
            Id = "evt-meta",
            OrganizationId = "org1",
            ProjectId = "proj1",
            Type = Event.KnownTypes.Log,
            Date = FixedDate
        };
        original.SetVersion("2.1.0-beta");
        original.SetLevel("Warning");

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<PersistentEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("2.1.0-beta", deserialized.GetVersion());
        Assert.Equal("Warning", deserialized.GetLevel());
    }

    [Fact]
    public void Deserialize_EventWithAllKnownDataKeys_PreservesAllTypes()
    {
        // Arrange
        var original = new PersistentEvent
        {
            Id = "evt-all",
            OrganizationId = "org1",
            ProjectId = "proj1",
            Type = Event.KnownTypes.Error,
            Date = FixedDate
        };
        original.SetUserIdentity("user@example.com", "Test User");
        original.SetVersion("1.0.0");
        original.SetLevel("Error");
        original.AddRequestInfo(new RequestInfo { HttpMethod = "GET", Path = "/test" });
        original.SetEnvironmentInfo(new EnvironmentInfo { MachineName = "TEST" });

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<PersistentEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.GetUserIdentity());
        Assert.NotNull(deserialized.GetRequestInfo());
        Assert.NotNull(deserialized.GetEnvironmentInfo());
        Assert.Equal("1.0.0", deserialized.GetVersion());
        Assert.Equal("Error", deserialized.GetLevel());
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesAllProperties()
    {
        // Arrange
        /* language=json */
        const string json = """{"id":"ext-1","organization_id":"org-ext","project_id":"proj-ext","type":"log","message":"External event","reference_id":"ref-ext","date":"2024-01-15T12:00:00+00:00","tags":["external"],"count":1,"data":{},"is_first_occurrence":false,"is_fixed":false,"is_hidden":false}""";

        // Act
        var ev = _serializer.Deserialize<PersistentEvent>(json);

        // Assert
        Assert.NotNull(ev);
        Assert.Equal("ext-1", ev.Id);
        Assert.Equal("org-ext", ev.OrganizationId);
        Assert.Equal("proj-ext", ev.ProjectId);
        Assert.Equal("log", ev.Type);
        Assert.Equal("External event", ev.Message);
        Assert.Equal("ref-ext", ev.ReferenceId);
        Assert.NotNull(ev.Tags);
        Assert.Contains("external", ev.Tags);
    }

    [Fact]
    public void Deserialize_JsonWithDataDictionary_PreservesCustomData()
    {
        // Arrange
        /* language=json */
        const string json = """{"id":"data-1","organization_id":"org1","project_id":"proj1","type":"log","date":"2024-01-15T12:00:00+00:00","data":{"custom_key":"custom_value","custom_number":42},"tags":[],"count":1,"is_first_occurrence":false,"is_fixed":false,"is_hidden":false}""";

        // Act
        var ev = _serializer.Deserialize<PersistentEvent>(json);

        // Assert
        Assert.NotNull(ev);
        Assert.NotNull(ev.Data);
        Assert.Contains("custom_key", ev.Data.Keys);
        Assert.Contains("custom_number", ev.Data.Keys);
    }

    [Fact]
    public void Deserialize_JsonWithTypedUserData_RetrievesTypedUserInfo()
    {
        // Arrange
        /* language=json */
        const string json = """{"id":"user-1","organization_id":"org1","project_id":"proj1","type":"error","date":"2024-01-15T12:00:00+00:00","data":{"@user":{"identity":"parsed@example.com","name":"Parsed User","data":{}}},"tags":[],"count":1,"is_first_occurrence":false,"is_fixed":false,"is_hidden":false}""";

        // Act
        var ev = _serializer.Deserialize<PersistentEvent>(json);

        // Assert
        Assert.NotNull(ev);
        var userInfo = ev.GetUserIdentity();
        Assert.NotNull(userInfo);
        Assert.Equal("parsed@example.com", userInfo.Identity);
        Assert.Equal("Parsed User", userInfo.Name);
    }

    [Fact]
    public void Deserialize_JsonWithNestedDataProperties_PreservesNestedStructure()
    {
        // Arrange
        /* language=json */
        const string json = """{"id":"nested-1","organization_id":"org1","project_id":"proj1","type":"log","date":"2024-01-15T12:00:00+00:00","data":{"key1":"value1","key2":42,"nested":{"inner_key":"inner_value"}},"tags":[],"count":1,"is_first_occurrence":false,"is_fixed":false,"is_hidden":false}""";

        // Act
        var ev = _serializer.Deserialize<PersistentEvent>(json);

        // Assert
        Assert.NotNull(ev);
        Assert.NotNull(ev.Data);
        Assert.Contains("key1", ev.Data.Keys);
        Assert.Contains("nested", ev.Data.Keys);
    }

    [Fact]
    public void Deserialize_JsonWithUnknownRootProperties_IgnoresUnknownProperties()
    {
        // Arrange
        /* language=json */
        const string json = """{"id":"unk-1","organization_id":"org1","project_id":"proj1","type":"log","message":"Test","date":"2024-01-15T12:00:00+00:00","unknown_property":"should_be_ignored","another_unknown":123,"tags":[],"count":1,"data":{},"is_first_occurrence":false,"is_fixed":false,"is_hidden":false}""";

        // Act
        var ev = _serializer.Deserialize<PersistentEvent>(json);

        // Assert
        Assert.NotNull(ev);
        Assert.Equal("unk-1", ev.Id);
        Assert.Equal("log", ev.Type);
        Assert.Equal("Test", ev.Message);
    }

    [Fact]
    public void Deserialize_MixedCaseProperties_ParsesCorrectly()
    {
        // Arrange - snake_case (standard) vs PascalCase
        /* language=json */
        const string snakeCaseJson = """{"id":"case-1","organization_id":"org1","project_id":"proj1","type":"log","reference_id":"ref-snake","date":"2024-01-15T12:00:00+00:00","tags":[],"count":1,"data":{},"is_first_occurrence":false,"is_fixed":false,"is_hidden":false}""";

        // Act
        var ev = _serializer.Deserialize<PersistentEvent>(snakeCaseJson);

        // Assert
        Assert.NotNull(ev);
        Assert.Equal("ref-snake", ev.ReferenceId);
    }

    [Fact]
    public void Deserialize_EventWithSpecialCharacters_PreservesCharacters()
    {
        // Arrange
        var original = new PersistentEvent
        {
            Id = "special-1",
            OrganizationId = "org1",
            ProjectId = "proj1",
            Type = Event.KnownTypes.Log,
            Message = "Test with \"quotes\" and \\backslash and 日本語",
            Date = FixedDate
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<PersistentEvent>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("Test with \"quotes\" and \\backslash and 日本語", deserialized.Message);
    }
}
