using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Nest;

namespace Exceptionless.Core.Repositories;

public class ProjectRepository : RepositoryOwnedByOrganization<Project>, IProjectRepository
{
    private readonly TimeProvider _timeProvider;

    public ProjectRepository(ExceptionlessElasticConfiguration configuration, IValidator<Project> validator, AppOptions options)
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

        return FindAsync(q => q.Organization(organizationIds).SortAscending(p => p.Name.Suffix("keyword")), options);
    }

    public Task<FindResults<Project>> GetByFilterAsync(AppFilter systemFilter, string? userFilter, string? sort, CommandOptionsDescriptor<Project>? options = null)
    {
        IRepositoryQuery<Project> query = new RepositoryQuery<Project>()
            .AppFilter(systemFilter)
            .FilterExpression(userFilter);

        query = !String.IsNullOrEmpty(sort) ? query.SortExpression(sort) : query.SortAscending(p => p.Name.Suffix("keyword"));
        return FindAsync(q => query, options);
    }

    public Task<FindResults<Project>> GetByNextSummaryNotificationOffsetAsync(byte hourToSendNotificationsAfterUtcMidnight, int limit = 50)
    {
        var filter = Query<Project>.Range(r => r.Field(o => o.NextSummaryEndOfDayTicks).LessThan(_timeProvider.GetUtcNow().UtcDateTime.Ticks - (TimeSpan.TicksPerHour * hourToSendNotificationsAfterUtcMidnight)));
        return FindAsync(q => q.ElasticFilter(filter).SortAscending(p => p.OrganizationId), o => o.SearchAfterPaging().PageLimit(limit));
    }

    public async Task IncrementNextSummaryEndOfDayTicksAsync(IReadOnlyCollection<Project> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        if (projects.Count == 0)
            return;

        string script = $"ctx._source.next_summary_end_of_day_ticks += {TimeSpan.TicksPerDay}L;";
        await PatchAsync(projects.Select(p => p.Id).ToArray(), new ScriptPatch(script), o => o.Notifications(false));
        await InvalidateCacheAsync(projects);
    }

    protected override async Task AddDocumentsToCacheAsync(ICollection<FindHit<Project>> findHits, ICommandOptions options, bool isDirtyRead)
    {
        await base.AddDocumentsToCacheAsync(findHits, options, isDirtyRead);

        var cacheEntries = new Dictionary<string, Project>();
        foreach (var project in findHits.Select(hit => hit.Document).Where(d => !String.IsNullOrEmpty(d?.Id)))
            cacheEntries.Add(ConfigCacheKey(project.Id), ToCachedProjectConfig(project));

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
            Id = project.Id, OrganizationId = project.OrganizationId, Configuration = project.Configuration
        };
    }

    private static string ConfigCacheKey(string projectId) => String.Concat("config:", projectId);
    private static string OrganizationCacheKey(string organizationId) => String.Concat("Organization:", organizationId);
}
