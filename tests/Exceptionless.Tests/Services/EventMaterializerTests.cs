using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Services;
using System.Text.Json;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class EventMaterializerTests
{
    private readonly Organization _organization = new() { Id = "507f191e810c19729de860ea", Name = "Test" };
    private readonly Project _project = new()
    {
        Id = "507f191e810c19729de860eb",
        OrganizationId = "507f191e810c19729de860ea",
        Name = "Test"
    };
    private readonly EventMaterializer _materializer = new(new StackTraceParser(), TimeProvider.System);

    [Fact]
    public void Materialize_UnsupportedStackTrace_PreservesRawTrace()
    {
        const string rawStackTrace = "goroutine 1 [running]:\nmain.main()\n\t/app/main.go:10 +0x20";
        var source = new EventIngestionV3Event
        {
            Id = "event-1",
            Type = Event.KnownTypes.Error,
            ExceptionType = "panic",
            Message = "failed",
            StackTrace = rawStackTrace
        };

        PersistentEvent result = _materializer.Materialize(source, CreateFingerprint(), _organization, _project);

        var error = Assert.IsType<Error>(result.Data![Event.KnownDataKeys.Error]);
        Assert.Empty(error.StackTrace!);
        var simpleError = Assert.IsType<SimpleError>(result.Data[Event.KnownDataKeys.SimpleError]);
        Assert.Equal(rawStackTrace, simpleError.StackTrace);
        Assert.Equal("panic", simpleError.Type);
        Assert.Equal("failed", simpleError.Message);
    }

    [Fact]
    public void Materialize_ParsedStackTrace_DoesNotDuplicateRawTrace()
    {
        var source = new EventIngestionV3Event
        {
            Id = "event-1",
            Type = Event.KnownTypes.Error,
            StackTrace = "at Example.Service.Run() in /src/Service.cs:line 42"
        };

        PersistentEvent result = _materializer.Materialize(source, CreateFingerprint(), _organization, _project);

        var error = Assert.IsType<Error>(result.Data![Event.KnownDataKeys.Error]);
        Assert.Single(error.StackTrace!);
        Assert.False(result.Data.ContainsKey(Event.KnownDataKeys.SimpleError));
    }

    [Fact]
    public void Materialize_PartiallyParsedStackTrace_PreservesRawTrace()
    {
        const string rawStackTrace = "at Example.Service.Run() in /src/Service.cs:line 42\nasync boundary the parser does not understand";
        var source = new EventIngestionV3Event
        {
            Id = "event-1",
            Type = Event.KnownTypes.Error,
            ExceptionType = "Example.PartialException",
            Message = "Mixed stack",
            StackTrace = rawStackTrace
        };

        PersistentEvent result = _materializer.Materialize(source, CreateFingerprint(), _organization, _project);

        var error = Assert.IsType<Error>(result.Data![Event.KnownDataKeys.Error]);
        Assert.Single(error.StackTrace!);
        var simpleError = Assert.IsType<SimpleError>(result.Data[Event.KnownDataKeys.SimpleError]);
        Assert.Equal(rawStackTrace, simpleError.StackTrace);
    }

    [Fact]
    public void Materialize_RequestData_AppliesDefaultAndProjectExclusionsBeforePersistence()
    {
        _project.Configuration.Settings[SettingsDictionary.KnownKeys.DataExclusions] = "password";
        var source = new EventIngestionV3Event
        {
            Id = "event-request",
            Type = Event.KnownTypes.Log,
            Request = new EventIngestionV3Request
            {
                Cookies = new Dictionary<string, string>
                {
                    ["ASP.NET_SessionId"] = "secret-session",
                    ["theme"] = "dark"
                },
                QueryString = new Dictionary<string, string>
                {
                    ["password"] = "secret-query",
                    ["page"] = "1"
                },
                PostData = JsonSerializer.Deserialize<JsonElement>("""
                    {
                      "profile": {
                        "password": "secret-post",
                        "display_name": "Ada"
                      },
                      "__RequestVerificationToken": "secret-token"
                    }
                    """)
            }
        };

        PersistentEvent result = _materializer.Materialize(source, CreateFingerprint(), _organization, _project);

        var request = Assert.IsType<RequestInfo>(result.Data![Event.KnownDataKeys.RequestInfo]);
        Assert.Equal("dark", Assert.Single(request.Cookies!).Value);
        Assert.Equal("1", Assert.Single(request.QueryString!).Value);
        var postData = Assert.IsAssignableFrom<IDictionary<string, object?>>(request.PostData);
        Assert.DoesNotContain("__RequestVerificationToken", postData.Keys, StringComparer.OrdinalIgnoreCase);
        var profile = Assert.IsAssignableFrom<IDictionary<string, object?>>(postData["profile"]);
        Assert.DoesNotContain("password", profile.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Ada", profile["display_name"]);
    }

    [Fact]
    public void Materialize_ValuesAtDurableLimits_PreservesEveryValue()
    {
        string message = new('m', EventIngestionV3Limits.MaximumMessageLength);
        string referenceId = new('r', EventIngestionV3Limits.MaximumReferenceIdLength);
        string tag = new('t', EventIngestionV3Limits.MaximumTagLength);
        string title = new('s', EventIngestionV3Limits.MaximumStackTitleLength);
        var source = new EventIngestionV3Event
        {
            Id = "event-boundaries",
            Type = Event.KnownTypes.Log,
            Message = message,
            ReferenceId = referenceId,
            Tags = [tag],
            Stacking = new EventIngestionV3Stacking
            {
                Title = title,
                SignatureData = new Dictionary<string, string> { ["key"] = "value" }
            }
        };
        var fingerprint = new StackFingerprint(
            "signature",
            source.Stacking.SignatureData,
            source.Stacking.Title);

        PersistentEvent result = _materializer.Materialize(source, fingerprint, _organization, _project);

        Assert.Equal(message, result.Message);
        Assert.Equal(referenceId, result.ReferenceId);
        Assert.Equal(tag, Assert.Single(result.Tags!));
        var stacking = Assert.IsType<ManualStackingInfo>(result.Data![Event.KnownDataKeys.ManualStackingInfo]);
        Assert.Equal(title, stacking.Title);
    }

    private static StackFingerprint CreateFingerprint() => new(
        "signature",
        new Dictionary<string, string> { ["Type"] = Event.KnownTypes.Error });
}
