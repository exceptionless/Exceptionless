using System.Text.Json;
using Foundatio.Caching;
using Foundatio.Lock;

namespace Exceptionless.Web.Assistant;

public sealed class AssistantConversationService(
    ICacheClient cacheClient,
    ILockProvider lockProvider,
    ILogger<AssistantConversationService> logger)
{
    private readonly ScopedCacheClient _cache = new(cacheClient, "AssistantConversation");

    public async Task<AssistantConversationState> BeginTurnAsync(
        string userId,
        string organizationId,
        string conversationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string key = GetKey(userId, organizationId, conversationId);
        AssistantConversationState? turn = null;
        bool started = await lockProvider.TryUsingAsync($"assistant-conversation:{key}", async () =>
        {
            var cached = await _cache.GetAsync<AssistantConversationState>(key);
            turn = (cached.HasValue ? cached.Value : new AssistantConversationState([])) with { TurnId = Guid.NewGuid().ToString("N") };

            // Every user turn consumes the previous question, even when its answer produces no tool results.
            // Only this caller receives the pending action, so concurrent turns cannot share its approval.
            if (!await _cache.SetAsync(key, turn with { PendingConfirmations = [] }, TimeSpan.FromMinutes(AssistantLimits.ConversationRetentionMinutes)))
                throw new InvalidOperationException("Unable to save the assistant conversation turn.");
        }, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));

        if (!started || turn is null)
            throw new InvalidOperationException("Unable to start the assistant conversation turn.");

        return turn;
    }

    public async Task<bool> IsCurrentTurnAsync(string userId, string organizationId, string conversationId, string turnId)
    {
        var cached = await _cache.GetAsync<AssistantConversationState>(GetKey(userId, organizationId, conversationId));
        return cached.HasValue && cached.Value.TurnId == turnId;
    }

    public async Task AppendToolResultsAsync(
        string userId,
        string organizationId,
        string conversationId,
        string turnId,
        IReadOnlyCollection<AssistantConversationToolResult> toolResults,
        CancellationToken cancellationToken)
    {
        if (toolResults.Count == 0)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        string key = GetKey(userId, organizationId, conversationId);
        bool updated = await lockProvider.TryUsingAsync($"assistant-conversation:{key}", async () =>
        {
            var cached = await _cache.GetAsync<AssistantConversationState>(key);
            // A slow response from an earlier turn must not replace a newer question or restore consumed approval.
            if (!cached.HasValue || cached.Value.TurnId != turnId)
                return;

            var merged = cached.Value.ToolResults
                .Concat(toolResults)
                .DistinctBy(result => result.ToolCallId, StringComparer.Ordinal)
                .ToList();

            while (merged.Count > 0 && JsonSerializer.Serialize(merged).Length > AssistantLimits.MaximumToolContextCharacters)
                merged.RemoveAt(0);

            // Every action this turn held back stays pending, so one yes can confirm a request that covered several views.
            var pendingConfirmations = cached.Value.PendingConfirmations
                .Concat(toolResults.Where(result => AssistantConfirmation.IsRequiredResult(result.Result)))
                .DistinctBy(result => result.ToolCallId, StringComparer.Ordinal)
                .ToList();
            var state = cached.Value with { ToolResults = merged, PendingConfirmations = pendingConfirmations };
            await _cache.SetAsync(key, state, TimeSpan.FromMinutes(AssistantLimits.ConversationRetentionMinutes));
        }, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));

        if (!updated)
        {
            logger.LogWarning(
                "Unable to acquire the assistant conversation lock for user {UserId} in organization {OrganizationId}",
                userId,
                organizationId);
        }
    }

    private static string GetKey(string userId, string organizationId, string conversationId)
        => $"user:{userId}:organization:{organizationId}:conversation:{conversationId}";
}
