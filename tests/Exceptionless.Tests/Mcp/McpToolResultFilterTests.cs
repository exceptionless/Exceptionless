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
}
