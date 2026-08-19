using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Exceptionless.Web.Mcp;

public static class McpToolResultFilter
{
    public static CallToolResult MarkStructuredErrors(CallToolResult result)
    {
        if (result.StructuredContent is JsonElement structuredContent
            && structuredContent.ValueKind == JsonValueKind.Object
            && structuredContent.TryGetProperty("ok", out var ok)
            && ok.ValueKind == JsonValueKind.False)
        {
            result.IsError = true;
        }

        return result;
    }
}
