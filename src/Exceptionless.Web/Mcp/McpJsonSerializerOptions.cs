using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol;

namespace Exceptionless.Web.Mcp;

/// <summary>
/// Serializer options for MCP tool output.
/// </summary>
public static class McpJsonSerializerOptions
{
    public static JsonSerializerOptions Default { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
        options.TypeInfoResolver = options.TypeInfoResolver!.WithAddedModifier(WriteDatesAsUtc);
        options.MakeReadOnly();
        return options;
    }

    /// <summary>
    /// Writes every <see cref="DateTime"/> property as UTC so it carries a Z, as the output schema's date-time format
    /// (RFC 3339) requires. Older documents were stored without a time zone and read back as
    /// <see cref="DateTimeKind.Unspecified"/>; every stored date is UTC. Changing the getter instead of adding a
    /// converter keeps the generated output schema's date-time format.
    /// </summary>
    public static void WriteDatesAsUtc(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        foreach (var property in typeInfo.Properties)
        {
            if (property.Get is not { } get)
                continue;

            if (property.PropertyType == typeof(DateTime))
                property.Get = obj => ToUtc((DateTime)get(obj)!);
            else if (property.PropertyType == typeof(DateTime?))
                property.Get = obj => get(obj) is DateTime value ? ToUtc(value) : null;
        }
    }

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
