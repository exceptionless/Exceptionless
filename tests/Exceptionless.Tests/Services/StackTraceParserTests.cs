using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Foundatio.Serializer;
using System.Text.Json;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class StackTraceParserTests
{
    private readonly StackTraceParser _parser = new();

    [Theory]
    [InlineData("at Example.Services.OrderService.Run() in /src/OrderService.cs:line 42", "Example.Services", "OrderService", "Run", "/src/OrderService.cs", 42, null)]
    [InlineData("at com.example.OrderService.run(OrderService.java:18)", "com.example", "OrderService", "run", "OrderService.java", 18, null)]
    [InlineData("at OrderService.run (/app/order.js:12:7)", null, "OrderService", "run", "/app/order.js", 12, 7)]
    [InlineData("File \"/app/order.py\", line 9, in run", null, null, "run", "/app/order.py", 9, null)]
    public void Parse_KnownFormat_ProducesStructuredFrame(
        string stackTrace,
        string? expectedNamespace,
        string? expectedType,
        string expectedName,
        string expectedFile,
        int expectedLine,
        int? expectedColumn)
    {
        var frame = Assert.Single(_parser.Parse(stackTrace));

        Assert.Equal(expectedNamespace, frame.DeclaringNamespace);
        Assert.Equal(expectedType, frame.DeclaringType);
        Assert.Equal(expectedName, frame.Name);
        Assert.Equal(expectedFile, frame.FileName);
        Assert.Equal(expectedLine, frame.LineNumber);
        Assert.Equal(expectedColumn, frame.Column);
    }

    [Fact]
    public void Fingerprint_LineNumbersDiffer_ProducesSameSignature()
    {
        var service = new StackFingerprintService(_parser);
        var project = new Project();
        var organization = new Organization();
        var first = new EventIngestionV3Event
        {
            Id = "event-1",
            Type = Event.KnownTypes.Error,
            ExceptionType = "System.InvalidOperationException",
            StackTrace = "at System.Threading.Task.Run()\nat Example.OrderService.Save() in /src/OrderService.cs:line 10"
        };
        var second = first with
        {
            Id = "event-2",
            StackTrace = "at System.Threading.Task.Run()\nat Example.OrderService.Save() in /src/OrderService.cs:line 999"
        };

        StackFingerprint firstFingerprint = service.Create(first, organization, project);
        StackFingerprint secondFingerprint = service.Create(second, organization, project);

        Assert.Equal(firstFingerprint.SignatureHash, secondFingerprint.SignatureHash);
        Assert.Equal("Example.OrderService.Save()", firstFingerprint.SignatureData["Method"]);
        Assert.Equal(firstFingerprint.SignatureData.Values.ToSHA1(), firstFingerprint.SignatureHash);
    }

    [Fact]
    public void Fingerprint_UnsupportedFormat_UsesNormalizedTraceFallback()
    {
        var service = new StackFingerprintService(_parser);
        var organization = new Organization();
        var project = new Project();
        var first = new EventIngestionV3Event
        {
            Id = "event-1",
            Type = Event.KnownTypes.Error,
            ExceptionType = "Example.UnknownException",
            StackTrace = "opaque-frame offset 123 line 42"
        };

        StackFingerprint firstFingerprint = service.Create(first, organization, project);
        StackFingerprint lineNumberChange = service.Create(first with { StackTrace = "opaque-frame offset 999 line 7" }, organization, project);
        StackFingerprint semanticChange = service.Create(first with { StackTrace = "different-frame offset 123 line 42" }, organization, project);

        Assert.Equal(firstFingerprint.SignatureHash, lineNumberChange.SignatureHash);
        Assert.NotEqual(firstFingerprint.SignatureHash, semanticChange.SignatureHash);
        Assert.True(firstFingerprint.SignatureData.ContainsKey("StackTrace"));
        Assert.DoesNotContain("opaque-frame", firstFingerprint.SignatureData["StackTrace"]);
    }

    [Fact]
    public void Parse_MalformedLines_IgnoresUnrecognizedContent()
    {
        var frames = _parser.Parse("Exception happened\n--- end of inner exception ---\nnot a frame");

        Assert.Empty(frames);
    }

    [Fact]
    public void ParseError_JavaCausedBy_PreservesNestedException()
    {
        const string trace = """
            com.example.OuterException: outer
                at com.example.Outer.run(Outer.java:10)
            Caused by: com.example.InnerException: inner
                at com.example.Inner.fail(Inner.java:20)
            """;

        var error = _parser.ParseError(trace, "com.example.OuterException", "outer");

        Assert.Equal("com.example.OuterException", error.Type);
        Assert.Equal("Outer", Assert.Single(error.StackTrace!).DeclaringType);
        Assert.NotNull(error.Inner);
        Assert.Equal("com.example.InnerException", error.Inner.Type);
        Assert.Equal("inner", error.Inner.Message);
        Assert.Equal("Inner", Assert.Single(error.Inner.StackTrace!).DeclaringType);
    }

    [Fact]
    public void Fingerprint_CausedBy_UsesInnermostExceptionAndFrame()
    {
        var service = new StackFingerprintService(_parser);
        var source = new EventIngestionV3Event
        {
            Id = "event-1",
            Type = Event.KnownTypes.Error,
            ExceptionType = "com.example.OuterException",
            StackTrace = "at com.example.Outer.run(Outer.java:10)\nCaused by: com.example.InnerException: inner\nat com.example.Inner.fail(Inner.java:20)"
        };

        StackFingerprint fingerprint = service.Create(source, new Organization(), new Project());

        Assert.Equal("com.example.InnerException", fingerprint.SignatureData["ExceptionType"]);
        Assert.Equal("com.example.Inner.fail()", fingerprint.SignatureData["Method"]);
    }

    [Fact]
    public void Fingerprint_StructuredV2AndRawV3_ProduceSameSignature()
    {
        const string trace = "at System.Threading.Task.Run()\nat Example.OrderService.Save() in /src/OrderService.cs:line 42";
        var error = new Error
        {
            Type = "System.InvalidOperationException",
            StackTrace = _parser.Parse(trace)
        };
        var serializer = new SystemTextJsonSerializer(new JsonSerializerOptions());
        var v2 = new ErrorSignature(error, serializer, ["Example"], shouldFlagSignatureTarget: false);
        var source = new EventIngestionV3Event
        {
            Id = "event-1",
            Type = Event.KnownTypes.Error,
            ExceptionType = error.Type,
            StackTrace = trace
        };
        var project = new Project { Data = new DataDictionary { ["UserNamespaces"] = "Example" } };

        StackFingerprint v3 = new StackFingerprintService(_parser).Create(source, new Organization(), project);

        Assert.Equal(v2.SignatureHash, v3.SignatureHash);
        Assert.Equal(v2.SignatureInfo, v3.SignatureData);
    }

    [Fact]
    public void Fingerprint_ManualStacking_TakesPrecedenceOverRawTrace()
    {
        var source = new EventIngestionV3Event
        {
            Id = "event-1",
            Type = Event.KnownTypes.Error,
            ExceptionType = "Ignored.Exception",
            StackTrace = "at Ignored.Type.Run()",
            Stacking = new EventIngestionV3Stacking
            {
                Title = "Orders failed",
                SignatureData = new Dictionary<string, string> { ["OrderOperation"] = "Save" }
            }
        };

        StackFingerprint fingerprint = new StackFingerprintService(_parser).Create(source, new Organization(), new Project());

        Assert.Equal("Orders failed", fingerprint.Title);
        Assert.Equal("Save", fingerprint.SignatureData["OrderOperation"]);
        Assert.Single(fingerprint.SignatureData);
    }
}
