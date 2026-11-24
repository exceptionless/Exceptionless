using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Foundatio.Repositories;
using McSherry.SemanticVersioning;

namespace Exceptionless.Core.Services;

/// <summary>
/// Changes the state of stacks. Shared by the single-item stack endpoints and the bulk stack operations so both apply
/// the same rules and have identical side effects. Callers have already verified that the actor may access the stacks.
/// </summary>
public class StackActionService
{
    /// <summary>A snooze must end at least this far in the future.</summary>
    public static readonly TimeSpan MinimumSnoozeDuration = TimeSpan.FromMinutes(5);

    private readonly IStackRepository _stackRepository;
    private readonly SemanticVersionParser _semanticVersionParser;
    private readonly TimeProvider _timeProvider;

    public StackActionService(IStackRepository stackRepository, SemanticVersionParser semanticVersionParser, TimeProvider timeProvider)
    {
        _stackRepository = stackRepository;
        _semanticVersionParser = semanticVersionParser;
        _timeProvider = timeProvider;
    }

    /// <summary>Parses the optional version a stack was fixed in. Returns false when a version was given but is not valid.</summary>
    public bool TryParseVersion(string? version, out SemanticVersion? semanticVersion)
    {
        semanticVersion = null;
        if (String.IsNullOrEmpty(version))
            return true;

        semanticVersion = _semanticVersionParser.Parse(version);
        return semanticVersion is not null;
    }

    /// <summary>Returns why a snooze can't end at <paramref name="snoozeUntilUtc"/>, or null when it can.</summary>
    public string? ValidateSnooze(DateTime snoozeUntilUtc)
    {
        return snoozeUntilUtc < _timeProvider.GetUtcNow().UtcDateTime.Add(MinimumSnoozeDuration) ? "Must snooze for at least 5 minutes." : null;
    }

    /// <summary>Regressed and snoozed are set by the system and by the snooze action, never by a status change.</summary>
    public static bool CanChangeStatusTo(StackStatus status) => status is not (StackStatus.Regressed or StackStatus.Snoozed);

    public Task MarkFixedAsync(IReadOnlyCollection<Stack> stacks, SemanticVersion? version)
    {
        foreach (var stack in stacks)
            stack.MarkFixed(version, _timeProvider);

        return _stackRepository.SaveAsync(stacks);
    }

    public Task SnoozeAsync(IReadOnlyCollection<Stack> stacks, DateTime snoozeUntilUtc)
    {
        foreach (var stack in stacks)
        {
            stack.SetStatus(StackStatus.Snoozed);
            stack.SnoozeUntilUtc = snoozeUntilUtc;
            stack.FixedInVersion = null;
            stack.DateFixed = null;
        }

        return _stackRepository.SaveAsync(stacks);
    }

    public async Task ChangeStatusAsync(IReadOnlyCollection<Stack> stacks, StackStatus status)
    {
        var changed = stacks.Where(s => s.Status != status).ToList();
        if (changed.Count is 0)
            return;

        foreach (var stack in changed)
        {
            stack.SetStatus(status);
            if (status is StackStatus.Fixed)
            {
                stack.DateFixed = _timeProvider.GetUtcNow().UtcDateTime;
            }
            else
            {
                stack.DateFixed = null;
                stack.FixedInVersion = null;
            }

            stack.SnoozeUntilUtc = null;
        }

        await _stackRepository.SaveAsync(changed);
    }

    /// <summary>
    /// Soft deletes the stacks. Their events are removed by the cleanup job, the same way as for a stack removed from the API.
    /// </summary>
    public Task DeleteAsync(IReadOnlyCollection<Stack> stacks)
    {
        foreach (var stack in stacks)
            stack.IsDeleted = true;

        return _stackRepository.SaveAsync(stacks);
    }
}
