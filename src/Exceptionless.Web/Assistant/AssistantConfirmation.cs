using System.Text.Json;
using System.Text.Json.Nodes;

namespace Exceptionless.Web.Assistant;

/// <summary>
/// Actions that can't be undone or that everyone in the organization sees need the user's say-so in a later message.
/// The model reads text from events and tool results, so it can be talked into calling a tool, but it can't write the
/// user's next message. The prompt asking for this is only the courtesy; these checks are what enforce it.
/// </summary>
internal static class AssistantConfirmation
{
    /// <summary>What the follow-up button sends, so people writing in any language can confirm.</summary>
    public const string Prompt = "Yes, continue";

    private const string DeleteSavedViewTool = "delete_saved_view";
    private const string CreateSavedViewTool = "create_saved_view";
    private const string RequiredCode = "confirmation_required";

    private static readonly HashSet<string> s_confirmations = new(StringComparer.OrdinalIgnoreCase)
    {
        "y", "yes", "yep", "yeah", "ok", "okay", "sure", "confirm", "confirmed", "go ahead", "do it", "please do", "delete it", "create it",
        "yes please", "yes continue", "yes go ahead", "yes do it", "yes delete it", "yes create it"
    };

    public static bool IsRequired(string toolName, string arguments)
        => toolName == DeleteSavedViewTool || (toolName == CreateSavedViewTool && IsShared(arguments));

    public static bool IsConfirmed(string toolName, string arguments, AssistantChatRequest request, AssistantConversationToolResult? pendingConfirmation)
    {
        string? latestUserMessage = request.Messages.LastOrDefault(message => message.Role == "user")?.Content;
        if (!IsConfirmation(latestUserMessage)
            || (toolName == DeleteSavedViewTool && latestUserMessage!.Contains("create", StringComparison.OrdinalIgnoreCase))
            || (toolName == CreateSavedViewTool && latestUserMessage!.Contains("delete", StringComparison.OrdinalIgnoreCase)))
            return false;

        return pendingConfirmation is not null
            && pendingConfirmation.ToolName == toolName
            && HasSameArguments(pendingConfirmation.Arguments, arguments)
            && IsRequiredResult(pendingConfirmation.Result);
    }

    public static bool IsRequiredResult(string result)
    {
        try
        {
            using var document = JsonDocument.Parse(result);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False
                && root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
                && code.GetString() == RequiredCode;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string CreateRequiredResult(string toolName, JsonSerializerOptions options)
    {
        string action = toolName == DeleteSavedViewTool ? "Deleting this saved view" : "Creating a shared view";
        return JsonSerializer.Serialize(new
        {
            ok = false,
            error = new
            {
                code = RequiredCode,
                message = $"{action} needs the user's confirmation. Nothing has changed yet."
            }
        }, options);
    }

    /// <summary>Only a complete, plain yes counts, so "yes?" or "yes, but not yet" can't approve the action.</summary>
    internal static bool IsConfirmation(string? message)
        => message is not null && s_confirmations.Contains(message.Trim().TrimEnd('.', '!').Replace(",", String.Empty, StringComparison.Ordinal));

    /// <summary>
    /// Writes the current project into a shared view's arguments. Otherwise the same arguments would mean a different
    /// project if the user switched projects before confirming, and they would approve something they were never asked about.
    /// </summary>
    public static string ResolveProjectScope(string toolName, string arguments, string? currentProjectId)
    {
        if (toolName != CreateSavedViewTool || String.IsNullOrWhiteSpace(currentProjectId) || !IsShared(arguments))
            return arguments;

        var parsed = TryParse(arguments);
        if (parsed is null || (GetBoolean(parsed["allProjects"]) ?? GetBoolean(parsed["all_projects"])) is true || HasValues(parsed["projectIds"]) || HasValues(parsed["project_ids"]))
            return arguments;

        parsed.Remove("project_ids");
        parsed["projectIds"] = new JsonArray(currentProjectId);
        return parsed.ToJsonString();
    }

    private static bool IsShared(string arguments) => GetBoolean(TryParse(arguments)?["shared"]) is true;

    private static bool? GetBoolean(JsonNode? node) => node is JsonValue value && value.TryGetValue(out bool result) ? result : null;

    // Match argument execution: blank strings and non-string array entries do not override the current project.
    private static bool HasValues(JsonNode? node) => node is JsonArray values ? values.Any(HasValue) : HasValue(node);

    private static bool HasValue(JsonNode? node) => node is JsonValue value && value.TryGetValue(out string? result) && !String.IsNullOrWhiteSpace(result);

    private static bool HasSameArguments(string first, string second)
        => TryParse(first) is { } firstArguments && TryParse(second) is { } secondArguments && JsonNode.DeepEquals(firstArguments, secondArguments);

    private static JsonObject? TryParse(string arguments)
    {
        try
        {
            return JsonNode.Parse(arguments) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
