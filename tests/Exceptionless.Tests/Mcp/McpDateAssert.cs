using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Exceptionless.Tests.Mcp;

/// <summary>
/// Asserts MCP output dates satisfy JSON Schema's date-time format (RFC 3339), which requires a Z or an offset.
/// </summary>
internal static partial class McpDateAssert
{
    public static void AllDatesHaveTimeZone(JsonElement element, string path = "$")
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    AllDatesHaveTimeZone(property.Value, $"{path}.{property.Name}");
                break;
            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                    AllDatesHaveTimeZone(item, $"{path}[{index++}]");
                break;
            case JsonValueKind.String when DateTimePattern().IsMatch(element.GetString()!):
                Assert.True(TimeZonePattern().IsMatch(element.GetString()!), $"{path} has no time zone: {element.GetString()}");
                break;
        }
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}")]
    private static partial Regex DateTimePattern();

    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex TimeZonePattern();
}
