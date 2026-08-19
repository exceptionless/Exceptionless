using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public interface IRateNotificationRuleRepository : IRepositoryOwnedByOrganizationAndProject<RateNotificationRule>
{
    Task<FindResults<RateNotificationRule>> GetByProjectIdAndUserIdAsync(string projectId, string userId, CommandOptionsDescriptor<RateNotificationRule>? options = null);
    Task<FindResults<RateNotificationRule>> GetByOrganizationIdAndUserIdAsync(string organizationId, string userId, CommandOptionsDescriptor<RateNotificationRule>? options = null);
    Task<FindResults<RateNotificationRule>> GetEnabledByProjectIdAsync(string projectId, CommandOptionsDescriptor<RateNotificationRule>? options = null);
    Task<long> CountByProjectIdAndUserIdAsync(string projectId, string userId);
    Task<long> RemoveAllByOrganizationIdAndUserIdAsync(string organizationId, string userId);
}
