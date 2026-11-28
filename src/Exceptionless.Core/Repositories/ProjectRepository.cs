using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;

namespace Exceptionless.Core.Repositories;

public class ProjectRepository : RepositoryOwnedByOrganization<Project>, IProjectRepository
{
    private readonly TimeProvider _timeProvider;

    public ProjectRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : base(configuration.Projects, validator, options)
    {
        _timeProvider = configuration.TimeProvider;
        DocumentsChanging.AddSyncHandler(OnDocumentsChanging);
    }

    private void OnDocumentsChanging(object sender, DocumentsChangeEventArgs<Project> args)
    {
        foreach (var project in args.Documents)
            project.Value.TrimUsage(_timeProvider);
    }

    public async Task<Project?> GetConfigAsync(string? projectId)
    {
        if (String.IsNullOrEmpty(projectId))
            return null;

        string cacheKey = ConfigCacheKey(projectId);
        var configCacheValue = await Cache.GetAsync<Project>(cacheKey);
        if (configCacheValue.HasValue)
            return configCacheValue.Value;

        var project = await GetByIdAsync(projectId, o => o.ReadCache().Include(p => p.Configuration, p => p.OrganizationId));
        if (project is null)
            return null;

        // NOTE: We might read from cache, but we want to save a limited subset of data.
        await Cache.AddAsync(cacheKey, ToCachedProjectConfig(project));
        return project;
    }

    public Task<CountResult> GetCountByOrganizationIdAsync(string organizationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        return CountAsync(q => q.Organization(organizationId), o => o.Cache(OrganizationCacheKey(organizationId)));
    }

    public Task<FindResults<Project>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, CommandOptionsDescriptor<Project>? options = null)
    {
        ArgumentNullException.ThrowIfNull(organizationIds);

        if (organizationIds.Count == 0)
            return Task.FromResult(new FindResults<Project>());

        return FindAsync(q => q.Organization(organizationIds).SortAscending(p => p.Name), options);
    }

    public Task<FindResults<Project>> GetByFilterAsync(AppFilter systemFilter, string? userFilter, string? sort, CommandOptionsDescriptor<Project>? options = null)
    {
        IRepositoryQuery<Project> query = new RepositoryQuery<Project>()
            .AppFilter(systemFilter)
            .FilterExpression(userFilter);

        query = !String.IsNullOrEmpty(sort) ? query.SortExpression(sort) : query.SortAscending(p => p.Name);
        return FindAsync(q => query, options);
    }

    public Task<FindResults<Project>> GetByNextSummaryNotificationOffsetAsync(byte hourToSendNotificationsAfterUtcMidnight, int limit = 50)
    {
        long threshold = _timeProvider.GetUtcNow().UtcDateTime.Ticks - (TimeSpan.TicksPerHour * hourToSendNotificationsAfterUtcMidnight);
        return FindAsync(q => q
            .FieldLessThan(p => p.NextSummaryEndOfDayTicks, threshold)
            .SortAscending(p => p.OrganizationId), o => o.SearchAfterPaging().PageLimit(limit));
    }

    public async Task IncrementNextSummaryEndOfDayTicksAsync(IReadOnlyCollection<Project> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        if (projects.Count == 0)
            return;

        string script = $"ctx._source.next_summary_end_of_day_ticks += {TimeSpan.TicksPerDay}L;";
        long modifiedCount = await PatchAsync(projects.Select(p => p.Id).ToArray(), new ScriptPatch(script), o => o.Notifications(false));
        if (modifiedCount > 0)
            await InvalidateCacheAsync(projects);
    }

    public async Task<bool> SetIsConfiguredAsync(Project project, bool isConfigured)
    {
        ArgumentNullException.ThrowIfNull(project);

        // Patch only this field so concurrent project edits and deletions are preserved. An id-only
        // patch notification has no organization id, so web socket clients never receive it; publish
        // the change with the project's ownership instead. Clients refetch project lists when notified,
        // so the change must be searchable before the notification is published.
        bool modified = await PatchAsync(project.Id, new PartialPatch(new { is_configured = isConfigured }), o => o.ImmediateConsistency().Notifications(false));
        if (modified)
            await PublishChangeTypeMessageAsync(ChangeType.Saved, project);

        return modified;
    }

    // The environment edits below patch only the fields they change. The environments page sends several small edits back to back,
    // and saving a whole stale copy of the project would silently undo whichever edit finished first.

    public Task<bool> AddEnvironmentAsync(Project project, ProjectEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(environment);

        const string script = @"
if (ctx._source.environments == null) {
  ctx._source.environments = new ArrayList();
}

for (def environment : ctx._source.environments) {
  if (environment.name.toLowerCase() == params.key) {
    ctx.op = 'noop';
    return;
  }
}

if (ctx._source.environments.size() >= params.maximumCount) {
  ctx.op = 'noop';
  return;
}

ctx._source.environments.add(params.environment);
ctx._source.updated_utc = params.updatedUtc;";

        return PatchEnvironmentsAsync(project, new ScriptPatch(script.TrimScript())
        {
            Params = new Dictionary<string, object>(4)
            {
                { "key", ProjectEnvironment.GetKey(environment.Name) },
                { "maximumCount", ProjectEnvironment.MaximumCount },
                { "environment", new Dictionary<string, object> { { "name", environment.Name }, { "notify", environment.Notify }, { "created_utc", environment.CreatedUtc } } },
                { "updatedUtc", _timeProvider.GetUtcNow().UtcDateTime }
            }
        });
    }

    public Task<bool> SetEnvironmentNotifyAsync(Project project, string name, bool notify)
    {
        ArgumentNullException.ThrowIfNull(project);

        const string script = @"
boolean changed = false;
if (ctx._source.environments != null) {
  for (def environment : ctx._source.environments) {
    if (environment.name.toLowerCase() == params.key && environment.notify != params.notify) {
      environment.notify = params.notify;
      changed = true;
    }
  }
}

if (changed) {
  ctx._source.updated_utc = params.updatedUtc;
} else {
  ctx.op = 'noop';
}";

        return PatchEnvironmentsAsync(project, new ScriptPatch(script.TrimScript())
        {
            Params = new Dictionary<string, object>(3)
            {
                { "key", ProjectEnvironment.GetKey(name) },
                { "notify", notify },
                { "updatedUtc", _timeProvider.GetUtcNow().UtcDateTime }
            }
        });
    }

    public Task<bool> RemoveEnvironmentAsync(Project project, string name)
    {
        ArgumentNullException.ThrowIfNull(project);

        const string script = @"
if (ctx._source.environments != null && ctx._source.environments.removeIf(environment -> environment.name.toLowerCase() == params.key)) {
  ctx._source.updated_utc = params.updatedUtc;
} else {
  ctx.op = 'noop';
}";

        return PatchEnvironmentsAsync(project, new ScriptPatch(script.TrimScript())
        {
            Params = new Dictionary<string, object>(2)
            {
                { "key", ProjectEnvironment.GetKey(name) },
                { "updatedUtc", _timeProvider.GetUtcNow().UtcDateTime }
            }
        });
    }

    public Task<bool> SetEnvironmentSettingsAsync(Project project, bool? discardUnlistedEnvironments, bool? discardEventsWithoutEnvironment, bool? notifyOtherEnvironments)
    {
        ArgumentNullException.ThrowIfNull(project);

        var changes = new Dictionary<string, object>(4);
        if (discardUnlistedEnvironments is { } discardUnlisted)
            changes["discard_unlisted_environments"] = discardUnlisted;
        if (discardEventsWithoutEnvironment is { } discardWithout)
            changes["discard_events_without_environment"] = discardWithout;
        if (notifyOtherEnvironments is { } notifyOther)
            changes["notify_other_environments"] = notifyOther;

        if (changes.Count is 0)
            return Task.FromResult(false);

        changes["updated_utc"] = _timeProvider.GetUtcNow().UtcDateTime;
        return PatchEnvironmentsAsync(project, new PartialPatch(changes));
    }

    private async Task<bool> PatchEnvironmentsAsync(Project project, IPatchOperation operation)
    {
        // Like SetIsConfiguredAsync: an id-only patch notification has no organization id, so publish with the project's ownership.
        bool modified = await PatchAsync(project.Id, operation, o => o.ImmediateConsistency().Notifications(false));
        if (!modified)
            return false;

        // Ingestion reads cached projects, so drop every cached copy, including the client configuration entry.
        await InvalidateCacheAsync([project]);
        await PublishChangeTypeMessageAsync(ChangeType.Saved, project);
        return true;
    }

    protected override async Task AddDocumentsToCacheAsync(ICollection<FindHit<Project>> findHits, ICommandOptions options, bool isDirtyRead)
    {
        await base.AddDocumentsToCacheAsync(findHits, options, isDirtyRead);

        var cacheEntries = new Dictionary<string, Project>();
        foreach (var project in findHits.Select(hit => hit.Document).Where(d => !String.IsNullOrEmpty(d?.Id)))
            cacheEntries.Add(ConfigCacheKey(project!.Id), ToCachedProjectConfig(project));

        // NOTE: We call SetAllAsync instead of AddDocumentsToCacheWithKeyAsync due to our repo method gets the value directly from cache.
        if (cacheEntries.Count > 0)
            await Cache.SetAllAsync(cacheEntries, options.GetExpiresIn());
    }

    protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<Project>> documents, ChangeType? changeType = null)
    {
        var originalAndModifiedDocuments = documents.UnionOriginalAndModified();

        // Invalidate GetCountByOrganizationIdAsync
        var organizationIds = originalAndModifiedDocuments.Select(d => d.OrganizationId).Distinct().Where(id => !String.IsNullOrEmpty(id));
        var countByOrganizationKeysToRemove = organizationIds.Select(id => $"count:{OrganizationCacheKey(id)}");
        await Cache.RemoveAllAsync(countByOrganizationKeysToRemove);

        var configKeysToRemove = originalAndModifiedDocuments.Select(d => ConfigCacheKey(d.Id)).Distinct();
        await Cache.RemoveAllAsync(configKeysToRemove);

        await base.InvalidateCacheAsync(documents, changeType);
    }

    private static Project ToCachedProjectConfig(Project project)
    {
        return new Project
        {
            Id = project.Id,
            OrganizationId = project.OrganizationId,
            Configuration = project.Configuration
        };
    }

    private static string ConfigCacheKey(string projectId) => String.Concat("config:", projectId);
    private static string OrganizationCacheKey(string organizationId) => String.Concat("Organization:", organizationId);
}
