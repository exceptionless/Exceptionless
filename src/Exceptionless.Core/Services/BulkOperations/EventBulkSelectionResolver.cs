using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Extensions;
using Foundatio.Serializer;

namespace Exceptionless.Core.Services.BulkOperations;

/// <summary>
/// Pages the ids of events that match an organization or project scope, a filter and an absolute UTC window, using the
/// same query building as the events list so the selection is exactly what the user was looking at.
/// </summary>
public class EventBulkSelectionResolver : IBulkSelectionResolver
{
    private const string CreatedUtcField = "created_utc";
    private readonly IEventRepository _eventRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly PersistentEventQueryValidator _validator;
    private readonly ITextSerializer _serializer;

    public EventBulkSelectionResolver(IEventRepository eventRepository, IOrganizationRepository organizationRepository, IProjectRepository projectRepository,
        PersistentEventQueryValidator validator, ITextSerializer serializer)
    {
        _eventRepository = eventRepository;
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _validator = validator;
        _serializer = serializer;
    }

    public BulkOperationEntity Entity => BulkOperationEntity.Events;

    public async Task<BulkOperationCheckResult> ValidateSelectionAsync(Organization organization, BulkOperationSelection selection)
    {
        var result = await _validator.ValidateQueryAsync(selection.Filter);
        if (!result.IsValid)
            return BulkOperationCheckResult.Invalid("selection.filter", result.Message ?? "Invalid filter.");

        if (result.UsesPremiumFeatures && !organization.HasPremiumFeatures)
            return BulkOperationCheckResult.PlanLimit("Please upgrade your plan to use premium search features.");

        return BulkOperationCheckResult.Ok;
    }

    public async Task<long> CountAsync(BulkOperation operation, CancellationToken cancellationToken)
    {
        var scope = await GetScopeAsync(operation);
        if (scope is null)
            return 0;

        var result = await _eventRepository.CountAsync(q => BuildQuery(q, scope.Value.AppFilter, operation.Selection));
        return result.Total;
    }

    public async Task<BulkSelectionPage> GetPageAsync(BulkOperation operation, string? cursor, int size, CancellationToken cancellationToken)
    {
        var scope = await GetScopeAsync(operation);
        if (scope is null)
            return new BulkSelectionPage([], null);

        var results = await _eventRepository.FindAsync(
            q => BuildQuery(q, scope.Value.AppFilter, operation.Selection).Include(e => e.Id).SortExpression($"-{EventIndex.Alias.Date}"),
            o => o.SearchAfterToken(cursor, _serializer).PageLimit(size));

        string[] ids = results.Hits.Select(h => h.Id).Where(id => !String.IsNullOrEmpty(id)).Select(id => id!).ToArray();
        string? nextCursor = results.Hits.LastOrDefault()?.GetSortToken(_serializer);
        return new BulkSelectionPage(ids, ids.Length > 0 ? nextCursor : null);
    }

    private static T BuildQuery<T>(T query, AppFilter appFilter, BulkOperationSelection selection) where T : class, IRepositoryQuery<PersistentEvent>
    {
        query.AppFilter(appFilter)
            .FilterExpression(selection.Filter)
            .EnforceEventStackFilter();

        if (selection.UtcStart.HasValue || selection.UtcEnd.HasValue)
        {
            query.DateRange(selection.UtcStart, selection.UtcEnd, EventIndex.Alias.Date)
                .Index(selection.UtcStart, selection.UtcEnd);
        }

        // The event date is chosen by the client, so an old-dated event can still arrive after the operation was created.
        if (selection.UtcCreatedBefore.HasValue)
            query.DateRange(null, selection.UtcCreatedBefore, CreatedUtcField);

        return query;
    }

    private async Task<(AppFilter AppFilter, Organization Organization)?> GetScopeAsync(BulkOperation operation)
    {
        var organization = await _organizationRepository.GetByIdAsync(operation.OrganizationId, o => o.Cache());
        if (organization is null)
            return null;

        var validation = await _validator.ValidateQueryAsync(operation.Selection.Filter);
        AppFilter appFilter;
        if (!String.IsNullOrEmpty(operation.ProjectId))
        {
            var project = await _projectRepository.GetByIdAsync(operation.ProjectId, o => o.Cache());
            if (project is null || !String.Equals(project.OrganizationId, organization.Id, StringComparison.Ordinal))
                return null;

            appFilter = new AppFilter(project, organization);
        }
        else
        {
            appFilter = new AppFilter(organization);
        }

        appFilter.UsesPremiumFeatures = validation.UsesPremiumFeatures;
        return (appFilter, organization);
    }
}
