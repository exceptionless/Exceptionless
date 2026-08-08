namespace Exceptionless.Web.Assistant;

public sealed record AssistantChatRequest(
    IReadOnlyCollection<AssistantChatMessage> Messages,
    string? OrganizationId = null,
    string? ProjectId = null,
    string? Path = null,
    string? ConversationId = null);

public sealed record AssistantChatMessage(string Role, string Content);

public sealed record AssistantConversationToolResult(
    string ToolCallId,
    string ToolName,
    string Arguments,
    string Result,
    string? Path,
    DateTimeOffset CapturedAtUtc);

public sealed record AssistantConversationState(IReadOnlyCollection<AssistantConversationToolResult> ToolResults);

public sealed record AssistantAccessResponse(
    bool Enabled,
    bool HasAccess,
    bool UpgradeRequired,
    string? Message = null);

public sealed record AssistantSuggestedAction(string Label, string Prompt);

public sealed record AssistantStreamEvent(
    string Type,
    string? Text = null,
    string? ToolCallId = null,
    string? ToolName = null,
    string? Arguments = null,
    string? Result = null,
    string? Message = null,
    IReadOnlyCollection<AssistantSuggestedAction>? SuggestedActions = null)
{
    public static AssistantStreamEvent TextDelta(string text) => new("text_delta", Text: text);
    public static AssistantStreamEvent ToolCall(string id, string name, string arguments) => new("tool_call", ToolCallId: id, ToolName: name, Arguments: arguments);
    public static AssistantStreamEvent ToolResult(string id, string name, string result) => new("tool_result", ToolCallId: id, ToolName: name, Result: result);
    public static AssistantStreamEvent Suggestions(IReadOnlyCollection<AssistantSuggestedAction> actions) => new("suggested_actions", SuggestedActions: actions);
    public static AssistantStreamEvent Error(string message) => new("error", Message: message);
    public static AssistantStreamEvent Done() => new("done");
}
