using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Options;
using Foundatio.Serializer;

namespace Exceptionless.Core.Services.BulkOperations;

/// <summary>Pages the ids of the events that match the selection, newest first.</summary>
public class EventBulkSelectionResolver : EventQueryBulkSelectionResolver
{
    private readonly IEventRepository _eventRepository;

    public EventBulkSelectionResolver(IEventRepository eventRepository, IOrganizationRepository organizationRepository, IProjectRepository projectRepository,
        PersistentEventQueryValidator validator, ITextSerializer serializer, ExceptionlessElasticConfiguration configuration)
        : base(organizationRepository, projectRepository, validator, serializer, configuration)
    {
        _eventRepository = eventRepository;
    }

    public override BulkOperationEntity Entity => BulkOperationEntity.Events;

    protected override async Task<long> CountAsync(RepositoryQuery<PersistentEvent> query)
    {
        return (await _eventRepository.CountAsync(query)).Total;
    }

    protected override async Task<BulkSelectionPage> FindPageAsync(RepositoryQuery<PersistentEvent> query, string? cursor, int size)
    {
        // The id breaks ties between events of the same date so the cursor never skips or repeats one.
        query.SortExpression($"-{EventIndex.Alias.Date} id");

        var page = await _eventRepository.GetEventIdPageAsync(query, cursor, size);
        return new BulkSelectionPage(page.Ids.ToArray(), page.NextCursor);
    }
}
