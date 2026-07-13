using Foundatio.Caching;
using Foundatio.Lock;

namespace Exceptionless.Core.Services;

/// <summary>
/// Runs ingestion side effects with at-least-once crash semantics and suppresses completed retries.
/// Completion is recorded only after the effect succeeds, so an abandoned worker can never poison
/// a retry before doing the work.
/// </summary>
public sealed class IngestionSideEffectExecutor(
    ICacheClient cache,
    ILockProvider lockProvider,
    AppOptions options)
{
    public const string StatisticsStage = "statistics";
    public const string TerminalStage = "notifications";
    public const string ProjectConfiguredStage = "project-configured";

    private const int StatisticsStageFlag = IngestionStackUsageStore.StatisticsStageFlag;
    private const int NotificationsStageFlag = 1 << 1;

    public async Task<int> ExecuteAsync(
        string stage,
        string projectId,
        IReadOnlyCollection<string> identities,
        Func<IReadOnlyCollection<string>, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(stage);
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        ArgumentNullException.ThrowIfNull(identities);
        ArgumentNullException.ThrowIfNull(action);

        string[] uniqueIdentities = identities
            .Where(identity => !String.IsNullOrEmpty(identity))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (uniqueIdentities.Length == 0)
            return 0;

        int stageFlag = GetStageFlag(stage);
        if (stageFlag == 0)
            return await ExecuteStandaloneStageAsync(stage, uniqueIdentities, action, cancellationToken);

        var pendingStates = await GetPendingStatesAsync(stageFlag, projectId, uniqueIdentities);
        if (pendingStates.Count == 0)
            return 0;

        string[] lockKeys = pendingStates.Keys.Select(identity => GetStateLockKey(projectId, identity)).ToArray();
        await using (ILock locks = await lockProvider.AcquireAsync(
            lockKeys,
            cancellationToken: cancellationToken))
        {
            // A competing worker may have completed while this worker waited for its locks.
            pendingStates = await GetPendingStatesAsync(stageFlag, projectId, pendingStates.Keys);
            if (pendingStates.Count == 0)
                return 0;

            await action(pendingStates.Keys.ToArray());

            // Incrementing by the single missing bit is an atomic OR because this stage is
            // serialized by the per-event lock. Redis statistics settlement also atomically ORs
            // its bit, so either operation order preserves both flags without a stale read/write.
            long[] completedStates = await Task.WhenAll(pendingStates.Keys.Select(identity =>
                cache.IncrementAsync(
                    GetStateKey(projectId, identity),
                    stageFlag,
                    options.EventIngestionV3.IdempotencyWindow)));
            if (completedStates.Any(state => (((long)state) & stageFlag) == 0))
                throw new InvalidOperationException($"Unable to record completion for ingestion side-effect stage '{stage}'.");

            return pendingStates.Count;
        }
    }

    public async Task<IReadOnlySet<string>> GetCompletedIdentitiesAsync(
        string stage,
        string projectId,
        IReadOnlyCollection<string> identities)
    {
        ArgumentException.ThrowIfNullOrEmpty(stage);
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        ArgumentNullException.ThrowIfNull(identities);

        string[] uniqueIdentities = identities
            .Where(identity => !String.IsNullOrEmpty(identity))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (uniqueIdentities.Length == 0)
            return new HashSet<string>(StringComparer.Ordinal);

        int stageFlag = GetStageFlag(stage);
        if (stageFlag == 0)
        {
            if (!String.Equals(stage, ProjectConfiguredStage, StringComparison.Ordinal))
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown ingestion side-effect stage.");

            return await GetStandaloneCompletedIdentitiesAsync(stage, uniqueIdentities);
        }

        var keysByIdentity = uniqueIdentities.ToDictionary(
            identity => identity,
            identity => GetStateKey(projectId, identity),
            StringComparer.Ordinal);
        var states = await cache.GetAllAsync<int>(keysByIdentity.Values);
        return uniqueIdentities
            .Where(identity => states.TryGetValue(keysByIdentity[identity], out var state)
                && state is { HasValue: true }
                && (state.Value & stageFlag) == stageFlag)
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<int> ExecuteStandaloneStageAsync(
        string stage,
        IReadOnlyCollection<string> identities,
        Func<IReadOnlyCollection<string>, Task> action,
        CancellationToken cancellationToken)
    {
        if (!String.Equals(stage, ProjectConfiguredStage, StringComparison.Ordinal))
            throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown ingestion side-effect stage.");

        var pendingIdentities = await GetStandalonePendingIdentitiesAsync(stage, identities);
        if (pendingIdentities.Count == 0)
            return 0;

        string[] lockKeys = pendingIdentities.Select(identity => GetStandaloneLockKey(stage, identity)).ToArray();
        await using (ILock locks = await lockProvider.AcquireAsync(
            lockKeys,
            cancellationToken: cancellationToken))
        {
            // A competing worker may have completed while this worker waited for its locks.
            pendingIdentities = await GetStandalonePendingIdentitiesAsync(stage, pendingIdentities);
            if (pendingIdentities.Count == 0)
                return 0;

            await action(pendingIdentities);

            var completed = pendingIdentities.ToDictionary(
                identity => GetStandaloneCompletionKey(stage, identity),
                _ => true,
                StringComparer.Ordinal);
            int completedCount = await cache.SetAllAsync(completed, options.EventIngestionV3.IdempotencyWindow);
            if (completedCount != completed.Count)
                throw new InvalidOperationException($"Unable to record completion for ingestion side-effect stage '{stage}'.");

            return pendingIdentities.Count;
        }
    }

    private async Task<IReadOnlySet<string>> GetStandaloneCompletedIdentitiesAsync(
        string stage,
        IReadOnlyCollection<string> identities)
    {
        var keysByIdentity = identities.ToDictionary(
            identity => identity,
            identity => GetStandaloneCompletionKey(stage, identity),
            StringComparer.Ordinal);
        var states = await cache.GetAllAsync<bool>(keysByIdentity.Values);
        return identities
            .Where(identity => states.TryGetValue(keysByIdentity[identity], out var state) && state is { HasValue: true, Value: true })
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, int>> GetPendingStatesAsync(int stageFlag, string projectId, IEnumerable<string> identities)
    {
        var keysByIdentity = identities.ToDictionary(identity => identity, identity => GetStateKey(projectId, identity), StringComparer.Ordinal);
        var states = await cache.GetAllAsync<int>(keysByIdentity.Values);
        var pending = new Dictionary<string, int>(keysByIdentity.Count, StringComparer.Ordinal);
        foreach (var pair in keysByIdentity)
        {
            var state = states[pair.Value];
            int stateValue = state.HasValue ? state.Value : 0;
            if ((stateValue & stageFlag) == 0)
                pending[pair.Key] = stateValue;
        }

        return pending;
    }

    private async Task<List<string>> GetStandalonePendingIdentitiesAsync(string stage, IReadOnlyCollection<string> identities)
    {
        string[] completionKeys = identities.Select(identity => GetStandaloneCompletionKey(stage, identity)).ToArray();
        var states = await cache.GetAllAsync<bool>(completionKeys);
        var pending = new List<string>(identities.Count);
        int index = 0;
        foreach (string identity in identities)
        {
            if (!states[completionKeys[index]].HasValue)
                pending.Add(identity);
            index++;
        }

        return pending;
    }

    private static int GetStageFlag(string stage) => stage switch
    {
        StatisticsStage => StatisticsStageFlag,
        TerminalStage => NotificationsStageFlag,
        _ => 0
    };

    private static string GetStateKey(string projectId, string identity) =>
        IngestionStackUsageStore.GetStateKey(projectId, identity);

    private static string GetStateLockKey(string projectId, string identity) =>
        IngestionStackUsageStore.GetStateLockKey(projectId, identity);

    private static string GetStandaloneCompletionKey(string stage, string identity) =>
        String.Concat("ingest-v3:sideeffects:", stage, ":", identity);

    private static string GetStandaloneLockKey(string stage, string identity) =>
        String.Concat("ingest-v3:sideeffects:lock:", stage, ":", identity);
}
