using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Nest;

namespace Exceptionless.Core.Repositories;

public class ProjectRepository : RepositoryOwnedByOrganization<Project>, IProjectRepository
{
    public ProjectRepository(ExceptionlessElasticConfiguration configuration, IValidator<Project> validator, AppOptions options)
        : base(configuration.Projects, validator, options)
    {
        DocumentsChanging.AddSyncHandler(OnDocumentsChanging);
    }

    private void OnDocumentsChanging(object sender, DocumentsChangeEventArgs<Project> args)
    {
        foreach (var project in args.Documents)
            project.Value.TrimUsage();
    }

    public async Task<Project?> GetConfigAsync(string? projectId)
    {
        if (String.IsNullOrEmpty(projectId))
            return null;

        var configCacheValue = await Cache.GetAsync<Project>($"config:{projectId}");
        if (configCacheValue.HasValue)
            return configCacheValue.Value;

        var project = await FindOneAsync(q => q.Id(projectId).Include(p => p.Configuration, p => p.OrganizationId));
        if (project?.Document is null)
            return null;

        await Cache.AddAsync($"config:{projectId}", project.Document);

        return project.Document;
    }

    public Task<CountResult> GetCountByOrganizationIdAsync(string organizationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        return CountAsync(q => q.Organization(organizationId), o => o.Cache(String.Concat("Organization:", organizationId)));
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

    protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<Project>> documents, ChangeType? changeType = null)
    {
        var organizations = documents.Select(d => d.Value.OrganizationId).Distinct().Where(id => !String.IsNullOrEmpty(id));
        await Cache.RemoveAllAsync(organizations.Select(id => $"count:Organization:{id}"));

        var configCacheKeys = documents.Select(d => $"config:{d.Value.Id}");
        await Cache.RemoveAllAsync(configCacheKeys);

        await base.InvalidateCacheAsync(documents, changeType);
    }
}
