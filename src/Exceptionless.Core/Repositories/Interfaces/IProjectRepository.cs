using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public interface IProjectRepository : IRepositoryOwnedByOrganization<Project>
{
    Task<FindResults<Project>> GetByNextSummaryNotificationOffsetAsync(byte hourToSendNotificationsAfterUtcMidnight, int limit = 50);
    Task IncrementNextSummaryEndOfDayTicksAsync(IReadOnlyCollection<Project> projects);
    Task<CountResult> GetCountByOrganizationIdAsync(string organizationId);
    Task<FindResults<Project>> GetByOrganizationIdsAsync(ICollection<string> organizationIds, CommandOptionsDescriptor<Project>? options = null);
    Task<FindResults<Project>> GetByFilterAsync(AppFilter systemFilter, string? userFilter, string? sort, CommandOptionsDescriptor<Project>? options = null);
    Task<Project?> GetConfigAsync(string? projectId);
    Task<bool> SetIsConfiguredAsync(Project project, bool isConfigured);
    Task<bool> AddEnvironmentAsync(Project project, ProjectEnvironment environment);
    Task<bool> SetEnvironmentNotifyAsync(Project project, string name, bool notify);
    Task<bool> RemoveEnvironmentAsync(Project project, string name);
    Task<bool> SetEnvironmentSettingsAsync(Project project, bool? discardUnlistedEnvironments, bool? discardEventsWithoutEnvironment, bool? notifyOtherEnvironments);
}
