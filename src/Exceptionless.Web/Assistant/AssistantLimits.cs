namespace Exceptionless.Web.Assistant;

internal static class AssistantLimits
{
    public const int MaximumInputMessages = 20;
    public const int MaximumInputCharacters = 48_000;
    public const int MaximumOutputTokens = 2048;
    public const int MaximumMalformedResponseRetries = 1;
    public const int MaximumToolRounds = 3;
    public const int MaximumToolCallsPerTurn = 12;
    public const int MaximumProjectsPerTurn = 5;
    public const int MaximumToolItemsPerCall = 10;
    public const int MaximumSuggestedActions = 3;
    public const int MaximumSuggestedActionLabelCharacters = 40;
    public const int MaximumSuggestedActionPromptCharacters = 300;
    public const int MaximumEventDetailCharacters = 16_384;
    public const int MaximumToolContextCharacters = 48_000;
    public const int MaximumProviderInputCharacters = 128_000;
    public const int MaximumTurnDurationSeconds = 120;
    public const int ConversationRetentionMinutes = 30;
    public const decimal MaximumProviderPromptPricePerMillionTokens = 2m;
    public const decimal MaximumProviderCompletionPricePerMillionTokens = 8m;
}
