using System.Text.Json;
using Exceptionless.Web.Mcp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Exceptionless.Tests.Mcp;

public sealed class McpToolResultFilterTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void MarkStructuredErrors_SetsProtocolErrorFromResponseEnvelope(bool ok, bool expectedIsError)
    {
        var result = new CallToolResult
        {
            Content = [],
            StructuredContent = JsonSerializer.SerializeToElement(new { ok })
        };

        var filtered = McpToolResultFilter.MarkStructuredErrors(result);

        Assert.Equal(expectedIsError, filtered.IsError ?? false);
    }

    [Fact]
    public void RejectUnknownArguments_RemovedTimeArgument_ReturnsErrorPointingToTime()
    {
        var schema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { filter = new { type = "string" }, time = new { type = "string" } } });

        var result = McpToolResultFilter.RejectUnknownArguments("search_stacks", schema, ["filter", "last"]);

        Assert.NotNull(result);
        Assert.True(result.IsError);
        var error = result.StructuredContent!.Value.GetProperty("error");
        Assert.Equal("invalid_argument", error.GetProperty("code").GetString());
        Assert.Contains("search_stacks does not accept last.", error.GetProperty("message").GetString());
        Assert.Contains("[now-24h TO now]", error.GetProperty("message").GetString());
        Assert.False(result.StructuredContent!.Value.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void RejectUnknownArguments_KnownArguments_ReturnsNull()
    {
        var schema = JsonSerializer.SerializeToElement(new { type = "object", properties = new { filter = new { type = "string" }, time = new { type = "string" } } });

        Assert.Null(McpToolResultFilter.RejectUnknownArguments("search_stacks", schema, ["filter", "time"]));
        Assert.Null(McpToolResultFilter.RejectUnknownArguments("search_stacks", schema, null));
    }
}
