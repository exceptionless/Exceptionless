using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Queries.Validation;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories;
using Foundatio.Serializer;

namespace Exceptionless.Core.Services.BulkOperations;

/// <summary>
/// Pages the ids of the stacks that have events matching the selection: the same stacks, filter and time window the stacks
/// list shows. The ids come from a composite aggregation on the events, so only one page of ids is held in memory at a time
/// and the cursor is the last stack id of the previous page.
/// </summary>
public class StackBulkSelectionResolver : EventQueryBulkSelectionResolver
{
    private readonly IEventRepository _eventRepository;

    public StackBulkSelectionResolver(IEventRepository eventRepository, IOrganizationRepository organizationRepository, IProjectRepository projectRepository,
        EventStackQueryValidator validator, ITextSerializer serializer, ExceptionlessElasticConfiguration configuration)
        : base(organizationRepository, projectRepository, validator, serializer, configuration)
    {
        _eventRepository = eventRepository;
    }

    public override BulkOperationEntity Entity => BulkOperationEntity.Stacks;

    protected override Task<long> CountAsync(RepositoryQuery<PersistentEvent> query)
    {
        return _eventRepository.CountStacksAsync(query);
    }

    protected override async Task<BulkSelectionPage> FindPageAsync(RepositoryQuery<PersistentEvent> query, string? cursor, int size)
    {
        var page = await _eventRepository.GetStackIdPageAsync(query, cursor, size);
        return new BulkSelectionPage(page.StackIds, page.NextAfterStackId);
    }
}
