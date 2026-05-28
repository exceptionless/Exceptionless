using Exceptionless.Core.Models.WorkItems;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories.Elasticsearch.CustomFields;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public class RemoveCustomFieldWorkItemHandler : WorkItemHandlerBase
{
    private readonly ICustomFieldDefinitionRepository _customFieldDefinitionRepository;
    private readonly ILockProvider _lockProvider;

    public RemoveCustomFieldWorkItemHandler(
        ICustomFieldDefinitionRepository customFieldDefinitionRepository,
        ILockProvider lockProvider,
        ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _customFieldDefinitionRepository = customFieldDefinitionRepository;
        _lockProvider = lockProvider;
    }

    public override Task<ILock?> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"{nameof(RemoveCustomFieldWorkItem)}:{((RemoveCustomFieldWorkItem)workItem).CustomFieldDefinitionId}";
        return _lockProvider.TryAcquireAsync(cacheKey, TimeSpan.FromMinutes(15), cancellationToken);
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var workItem = context.GetData<RemoveCustomFieldWorkItem>()!;

        using (Log.BeginScope(new ExceptionlessState().Organization(workItem.OrganizationId)))
        {
            Log.LogInformation("Processing custom field removal work item for definition: {DefinitionId}, field: {FieldName}",
                workItem.CustomFieldDefinitionId, workItem.FieldName);

            await context.ReportProgressAsync(0, "Acknowledging custom field soft-deletion...");

            // GetByIdAsync INCLUDES soft-deleted records (by-ID lookups bypass the soft-delete filter).
            // After the controller soft-deletes and enqueues this work item, GetByIdAsync returns the
            // definition with IsDeleted=true. A null result means the record has been physically removed.
            var definition = await _customFieldDefinitionRepository.GetByIdAsync(workItem.CustomFieldDefinitionId);

            if (definition is null)
            {
                Log.LogWarning(
                    "Custom field definition {DefinitionId} ('{FieldName}') no longer exists. " +
                    "It may have been hard-deleted externally.",
                    workItem.CustomFieldDefinitionId, workItem.FieldName);
            }
            else if (!definition.IsDeleted)
            {
                Log.LogWarning(
                    "Custom field definition {DefinitionId} ('{FieldName}') is unexpectedly active. " +
                    "It should have been soft-deleted before this work item was enqueued.",
                    workItem.CustomFieldDefinitionId, workItem.FieldName);
            }
            else
            {
                // Normal path: definition is soft-deleted as expected.
                // Hard-deletion (slot reclamation) is intentionally deferred to prevent slot-reuse corruption:
                //   recycling a slot before the org's retention window expires would cause historical events
                //   indexed under the old field to appear in queries for a new field assigned the same slot.
                // A future retention-aware cleanup job will hard-delete once all events using the old slot
                // have aged out beyond the org's retention period.
                Log.LogInformation(
                    "Custom field definition {DefinitionId} ('{FieldName}') is soft-deleted. " +
                    "Slot will be reclaimed by the retention cleanup job after the retention window expires.",
                    workItem.CustomFieldDefinitionId, workItem.FieldName);
            }

            await context.ReportProgressAsync(100, $"Custom field '{workItem.FieldName}' acknowledged.");
        }
    }
}
