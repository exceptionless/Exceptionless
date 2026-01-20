using Exceptionless.Core.Models;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests WebHook serialization through ITextSerializer.
/// Validates round-trip serialization and snake_case property naming.
/// </summary>
public class WebHookSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;
    private static readonly DateTime FixedDateTime = new(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    public WebHookSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void SerializeToString_WebHook_PreservesAllProperties()
    {
        // Arrange
        var hook = new WebHook
        {
            Id = "hook123",
            OrganizationId = "org456",
            ProjectId = "proj789",
            Url = "https://example.com/webhook",
            EventTypes = ["NewError", "CriticalError"],
            Version = WebHook.KnownVersions.Version2,
            IsEnabled = true,
            CreatedUtc = FixedDateTime
        };

        // Act
        string? json = _serializer.SerializeToString(hook);
        var deserialized = _serializer.Deserialize<WebHook>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("hook123", deserialized.Id);
        Assert.Equal("org456", deserialized.OrganizationId);
        Assert.Equal("proj789", deserialized.ProjectId);
        Assert.Equal("https://example.com/webhook", deserialized.Url);
        Assert.Equal(2, deserialized.EventTypes.Length);
        Assert.True(deserialized.IsEnabled);
    }

    [Fact]
    public void Deserialize_CompleteWebHook_PreservesAllProperties()
    {
        // Arrange
        var original = new WebHook
        {
            Id = "hook-rt",
            OrganizationId = "org1",
            ProjectId = "proj1",
            Url = "https://api.myapp.com/hooks/exceptionless",
            EventTypes = ["NewError", "StackRegression"],
            Version = WebHook.KnownVersions.Version2,
            IsEnabled = true,
            CreatedUtc = FixedDateTime
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<WebHook>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("hook-rt", deserialized.Id);
        Assert.Equal("https://api.myapp.com/hooks/exceptionless", deserialized.Url);
        Assert.Equal(2, deserialized.EventTypes.Length);
        Assert.True(deserialized.IsEnabled);
    }

    [Fact]
    public void Deserialize_MinimalWebHook_PreservesRequiredProperties()
    {
        // Arrange
        var original = new WebHook
        {
            Id = "hook1",
            EventTypes = ["NewError"],
            Version = WebHook.KnownVersions.Version2
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<WebHook>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("hook1", deserialized.Id);
        Assert.Single(deserialized.EventTypes);
        Assert.Equal("NewError", deserialized.EventTypes[0]);
    }

    [Fact]
    public void Deserialize_DisabledWebHook_PreservesDisabledState()
    {
        // Arrange
        var original = new WebHook
        {
            Id = "hook-disabled",
            Url = "https://example.com/disabled",
            EventTypes = ["NewError"],
            Version = WebHook.KnownVersions.Version2,
            IsEnabled = false
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<WebHook>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.False(deserialized.IsEnabled);
    }

    [Fact]
    public void Deserialize_WebHookWithAllEventTypes_PreservesAllEventTypes()
    {
        // Arrange
        var original = new WebHook
        {
            Id = "hook-all-events",
            Url = "https://example.com/all",
            EventTypes = ["NewError", "CriticalError", "StackRegression", "StackPromoted", "NewEvent"],
            Version = WebHook.KnownVersions.Version2
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<WebHook>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(5, deserialized.EventTypes.Length);
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesAllProperties()
    {
        // Arrange
        /* language=json */
        const string json = """{"id":"ext-hook","organization_id":"ext-org","project_id":"ext-proj","url":"https://external.com/hook","event_types":["NewError"],"is_enabled":true,"version":"v2","created_utc":"2024-01-15T12:00:00Z"}""";

        // Act
        var hook = _serializer.Deserialize<WebHook>(json);

        // Assert
        Assert.NotNull(hook);
        Assert.Equal("ext-hook", hook.Id);
        Assert.Equal("ext-org", hook.OrganizationId);
        Assert.Equal("https://external.com/hook", hook.Url);
        Assert.True(hook.IsEnabled);
    }

    [Fact]
    public void Deserialize_WebHookWithV1Version_PreservesVersion()
    {
        // Arrange
        var original = new WebHook
        {
            Id = "hook-v1",
            Url = "https://legacy.com/hook",
            EventTypes = ["NewError"],
            Version = WebHook.KnownVersions.Version1
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<WebHook>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(WebHook.KnownVersions.Version1, deserialized.Version);
    }
}
