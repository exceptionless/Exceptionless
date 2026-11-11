using System.Text.Json;

namespace Exceptionless.Web.Assistant;

/// <summary>
/// Deleting a view can't be undone, and changing a shared view changes it for everyone in the organization, so both
/// need the user's say-so in a later message. The model reads text from events and tool results, so it can be talked
/// into calling a tool, but it can't write the user's next message. The prompt asking for this is only the courtesy;
/// these checks are what enforce it.
/// </summary>
internal static class AssistantConfirmation
{
    /// <summary>What the follow-up button sends, so people writing in any language can confirm.</summary>
    public const string Prompt = "Yes, continue";

    private const string DeleteSavedViewTool = "delete_saved_view";
    private const string UpdateSavedViewTool = "update_saved_view";
    private const string RequiredCode = "confirmation_required";

    private static readonly HashSet<string> s_confirmations = new(StringComparer.OrdinalIgnoreCase)
    {
        "y", "yes", "yep", "yeah", "ok", "okay", "sure", "confirm", "confirmed", "go ahead", "do it", "do that", "please do",
        "delete it", "delete them", "change it", "update it",
        "yes please", "yes continue", "yes go ahead", "yes do it", "yes do that", "yes please do", "yes delete it", "yes delete them", "yes change it", "yes update it"
    };

    /// <summary>Deleting a view always asks first, and changing one asks only when it is shared.</summary>
    public static bool IsRequired(string toolName, bool isSharedView)
        => toolName == DeleteSavedViewTool || (toolName == UpdateSavedViewTool && isSharedView);

    /// <summary>The pending action this call repeats, when the user's latest message confirms it.</summary>
    public static AssistantConversationToolResult? FindConfirmed(string toolName, string arguments, AssistantChatRequest request, IEnumerable<AssistantConversationToolResult> pendingConfirmations)
    {
        string? latestUserMessage = request.Messages.LastOrDefault(message => message.Role == "user")?.Content;
        if (!IsConfirmation(latestUserMessage)
            || (toolName == UpdateSavedViewTool && latestUserMessage!.Contains("delete", StringComparison.OrdinalIgnoreCase))
            || (toolName == DeleteSavedViewTool && (latestUserMessage!.Contains("change", StringComparison.OrdinalIgnoreCase) || latestUserMessage.Contains("update", StringComparison.OrdinalIgnoreCase))))
            return null;

        return pendingConfirmations.FirstOrDefault(pending => pending.ToolName == toolName
            && HasSameArguments(pending.Arguments, arguments)
            && IsRequiredResult(pending.Result));
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
        string action = toolName == DeleteSavedViewTool
            ? "Deleting this saved view"
            : "Changing a shared view changes it for everyone in the organization, so it";
        return JsonSerializer.Serialize(new
        {
            ok = false,
            error = new
            {
                code = RequiredCode,
                message = $"{action} needs the user's confirmation. Nothing has changed yet. Ask the user, and when they confirm, call this tool again with the same arguments."
            }
        }, options);
    }

    /// <summary>Only a complete, plain yes counts, so "yes?" or "yes, but not yet" can't approve the action.</summary>
    internal static bool IsConfirmation(string? message)
        => message is not null && s_confirmations.Contains(message.Trim().TrimEnd('.', '!').Replace(",", String.Empty, StringComparison.Ordinal));

    /// <summary>Compared the way tool execution reads arguments, which accepts repeated property names instead of throwing.</summary>
    private static bool HasSameArguments(string first, string second)
    {
        try
        {
            using var firstArguments = JsonDocument.Parse(first);
            using var secondArguments = JsonDocument.Parse(second);
            return firstArguments.RootElement.ValueKind == JsonValueKind.Object && JsonElement.DeepEquals(firstArguments.RootElement, secondArguments.RootElement);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
