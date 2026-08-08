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

    public async Task<AssistantConversationState?> GetAsync(
        string userId,
        string organizationId,
        string conversationId)
    {
        string key = GetKey(userId, organizationId, conversationId);
        var value = await _cache.GetAsync<AssistantConversationState>(key);
        return value.HasValue ? value.Value : null;
    }

    public async Task AppendToolResultsAsync(
        string userId,
        string organizationId,
        string conversationId,
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
            var merged = (cached.HasValue ? cached.Value.ToolResults : [])
                .Concat(toolResults)
                .DistinctBy(result => result.ToolCallId, StringComparer.Ordinal)
                .ToList();

            while (merged.Count > 0 && JsonSerializer.Serialize(merged).Length > AssistantLimits.MaximumToolContextCharacters)
                merged.RemoveAt(0);

            var state = new AssistantConversationState(merged);
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
