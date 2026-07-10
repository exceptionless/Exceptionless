using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public sealed class RateNotificationRuleRepository : RepositoryOwnedByOrganizationAndProject<RateNotificationRule>, IRateNotificationRuleRepository
{
    public RateNotificationRuleRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : base(configuration.RateNotificationRules, validator, options)
    {
    }

    public Task<FindResults<RateNotificationRule>> GetByProjectIdAndUserIdAsync(string projectId, string userId, CommandOptionsDescriptor<RateNotificationRule>? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return FindAsync(q => q
            .Project(projectId)
            .FieldEquals(r => r.UserId, userId)
            .SortAscending(r => r.Name), options);
    }

    public Task<FindResults<RateNotificationRule>> GetByOrganizationIdAndUserIdAsync(string organizationId, string userId, CommandOptionsDescriptor<RateNotificationRule>? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return FindAsync(q => q
            .Organization(organizationId)
            .FieldEquals(r => r.UserId, userId)
            .SortAscending(r => r.Id), options);
    }

    public Task<FindResults<RateNotificationRule>> GetEnabledByProjectIdAsync(string projectId, CommandOptionsDescriptor<RateNotificationRule>? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);

        return FindAsync(q => q
            .Project(projectId)
            .FieldEquals(r => r.IsEnabled, true)
            .FieldEquals(r => r.IsDeleted, false)
            .SortAscending(r => r.Id), options);
    }

    public async Task<long> CountByProjectIdAndUserIdAsync(string projectId, string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return await CountAsync(q => q
            .Project(projectId)
            .FieldEquals(r => r.UserId, userId));
    }

    public Task<long> RemoveAllByOrganizationIdAndUserIdAsync(string organizationId, string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return RemoveAllAsync(q => q
            .Organization(organizationId)
            .FieldEquals(r => r.UserId, userId));
    }
}
