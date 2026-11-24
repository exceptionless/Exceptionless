using System.Text.Json;
using Exceptionless.Web.Api.Endpoints;
using Xunit;

namespace Exceptionless.Tests.Api;

public sealed class CspReportEndpointsTests
{
    [Fact]
    public void Parse_VersionedScript_StacksWithoutQueryAndKeepsDetails()
    {
        // Arrange
        var report = CreateReport("""
            {"document-uri":"https://app.localhost/reset-password?token=secret","effective-directive":"script-src-elem",
             "blocked-uri":"https://app.localhost/_app/env.js?v=568babb04cda5b7985c59ecbe00683a3",
             "source-file":"https://app.localhost/login","line-number":107,"disposition":"enforce"}
            """);

        // Act
        var violation = CspReportEndpoints.Parse(report);

        // Assert
        Assert.NotNull(violation);
        Assert.Equal("CSP blocked https://app.localhost/_app/env.js (script-src-elem)", violation.Title);
        Assert.Equal("https://app.localhost/_app/env.js", violation.Blocked);
        Assert.Equal("https://app.localhost/reset-password", violation.DocumentUri);
        Assert.Equal(107, violation.LineNumber);
        Assert.False(violation.IsIgnored);
        Assert.DoesNotContain(violation.GetProperties("agent"), property => property.Value.ToString()!.Contains("secret"));
    }

    [Fact]
    public void Parse_InlineScript_StacksBySourceFile()
    {
        // Arrange
        var report = CreateReport("""{"violated-directive":"script-src","blocked-uri":"inline","source-file":"https://app.localhost/login?next=1"}""");

        // Act
        var violation = CspReportEndpoints.Parse(report);

        // Assert
        Assert.NotNull(violation);
        Assert.Equal("CSP blocked inline from https://app.localhost/login (script-src)", violation.Title);
    }

    [Theory]
    [InlineData("""{"blocked-uri":"chrome-extension://abc/inject.js","effective-directive":"script-src-elem"}""")]
    [InlineData("""{"blocked-uri":"inline","source-file":"moz-extension://abc/content.js","effective-directive":"script-src-elem"}""")]
    [InlineData("""{"blocked-uri":"eval","source-file":"https://app.localhost/docs/scalar.js","effective-directive":"script-src"}""")]
    public void Parse_ExtensionOrKnownBenignReport_IsIgnored(string body)
    {
        // Arrange
        var report = CreateReport(body);

        // Act
        var violation = CspReportEndpoints.Parse(report);

        // Assert
        Assert.NotNull(violation);
        Assert.True(violation.IsIgnored);
    }

    [Theory]
    [InlineData("""{"unexpected":true}""")]
    [InlineData("""[1,2,3]""")]
    [InlineData("""{"csp-report":"text"}""")]
    public void Parse_NonReport_ReturnsNull(string json)
    {
        // Arrange
        using var document = JsonDocument.Parse(json);

        // Act
        var violation = CspReportEndpoints.Parse(document.RootElement.Clone());

        // Assert
        Assert.Null(violation);
    }

    private static JsonElement CreateReport(string body)
    {
        using var document = JsonDocument.Parse($$"""{"csp-report":{{body}}}""");
        return document.RootElement.Clone();
    }
}
