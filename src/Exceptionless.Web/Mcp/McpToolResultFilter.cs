using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Exceptionless.Web.Mcp;

public static class McpToolResultFilter
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] s_removedTimeArguments = ["last", "startUtc", "endUtc"];

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

    /// <summary>
    /// Rejects arguments a tool does not define. Binding would otherwise drop them, so a client still sending a
    /// removed argument such as last would get unfiltered results instead of an error.
    /// </summary>
    public static CallToolResult? RejectUnknownArguments(string toolName, JsonElement inputSchema, IEnumerable<string>? argumentNames)
    {
        if (argumentNames is null || inputSchema.ValueKind != JsonValueKind.Object || !inputSchema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
            return null;

        string[] allowed = properties.EnumerateObject().Select(property => property.Name).ToArray();
        string[] unknown = argumentNames.Where(name => !allowed.Contains(name, StringComparer.Ordinal)).ToArray();
        if (unknown.Length == 0)
            return null;

        string message = $"{toolName} does not accept {String.Join(", ", unknown)}.";
        if (unknown.Any(name => s_removedTimeArguments.Contains(name, StringComparer.Ordinal)) && allowed.Contains("time", StringComparer.Ordinal))
            message += " Pass a time range in time instead, using date math such as [now-24h TO now] or [now/d TO now/d] for today.";

        var response = McpResponse<object>.Failed(McpErrors.InvalidArgument(message, unknown, allowed));
        var structuredContent = JsonSerializer.SerializeToElement(response, s_jsonOptions);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = structuredContent.GetRawText() }],
            StructuredContent = structuredContent,
            IsError = true
        };
    }
}
