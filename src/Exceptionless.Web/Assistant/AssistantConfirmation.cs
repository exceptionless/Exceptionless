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
        "y", "yep", "yeah", "ok", "okay", "sure", "confirm", "confirmed", "go ahead", "do it", "please do", "delete it", "create it"
    };

    public static bool IsRequired(string toolName, string arguments)
        => toolName == DeleteSavedViewTool || (toolName == CreateSavedViewTool && IsShared(arguments));

    public static bool IsConfirmed(string toolName, string arguments, AssistantChatRequest request, AssistantConversationState? conversationState)
    {
        string? latestUserMessage = request.Messages.LastOrDefault(message => message.Role == "user")?.Content;
        if (!IsConfirmation(latestUserMessage))
            return false;

        // Only the latest attempt at this exact action counts, so confirming one view can't approve a different one.
        var previousAttempt = conversationState?.ToolResults.LastOrDefault(result => result.ToolName == toolName && HasSameArguments(result.Arguments, arguments));
        return previousAttempt?.Result.Contains(RequiredCode, StringComparison.Ordinal) is true;
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

    internal static bool IsConfirmation(string? message)
    {
        string normalized = message?.Trim().TrimEnd('.', '!') ?? String.Empty;
        if (s_confirmations.Contains(normalized))
            return true;

        return normalized.StartsWith("yes", StringComparison.OrdinalIgnoreCase) && (normalized.Length == 3 || !Char.IsLetter(normalized[3]));
    }

    private static bool IsShared(string arguments)
        => TryParse(arguments)?["shared"] is JsonValue shared && shared.TryGetValue(out bool value) && value;

    private static bool HasSameArguments(string first, string second)
        => JsonNode.DeepEquals(TryParse(first), TryParse(second));

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
