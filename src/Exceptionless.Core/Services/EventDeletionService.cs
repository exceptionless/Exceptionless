using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

/// <summary>
/// Removes events and records the deleted usage. Shared by the single-item delete endpoint and the bulk delete
/// operation so both have identical side effects.
/// </summary>
public class EventDeletionService
{
    private readonly IEventRepository _eventRepository;
    private readonly UsageService _usageService;
    private readonly ILogger<EventDeletionService> _logger;

    public EventDeletionService(IEventRepository eventRepository, UsageService usageService, ILogger<EventDeletionService> logger)
    {
        _eventRepository = eventRepository;
        _usageService = usageService;
        _logger = logger;
    }

    /// <summary>Deletes events the caller has already verified access to.</summary>
    public async Task DeleteAsync(IReadOnlyCollection<PersistentEvent> events)
    {
        if (events.Count == 0)
            return;

        await _eventRepository.RemoveAsync(events);

        foreach (var projectGroup in events.GroupBy(ev => new { ev.OrganizationId, ev.ProjectId }))
        {
            try
            {
                await _usageService.IncrementDeletedAsync(projectGroup.Key.OrganizationId, projectGroup.Key.ProjectId, projectGroup.Count());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to increment deleted usage metrics for organization {OrganizationId} project {ProjectId}: {Message}", projectGroup.Key.OrganizationId, projectGroup.Key.ProjectId, ex.Message);
            }
        }
    }
}
