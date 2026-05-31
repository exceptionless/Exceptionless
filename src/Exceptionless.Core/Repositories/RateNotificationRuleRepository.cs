using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Nest;

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
            .SortAscending(r => r.Name.Suffix("keyword")), options);
    }

    public Task<FindResults<RateNotificationRule>> GetEnabledByProjectIdAsync(string projectId, CommandOptionsDescriptor<RateNotificationRule>? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);

        return FindAsync(q => q
            .Project(projectId)
            .FieldEquals(r => r.IsEnabled, true)
            .FieldEquals(r => r.IsDeleted, false), options);
    }

    public async Task<long> CountByProjectIdAndUserIdAsync(string projectId, string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return await CountAsync(q => q
            .Project(projectId)
            .FieldEquals(r => r.UserId, userId));
    }

    public async Task<long> RemoveByProjectIdAndUserIdAsync(string projectId, string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var results = await FindAsync(q => q
            .Project(projectId)
            .FieldEquals(r => r.UserId, userId), o => o.PageLimit(1000));

        if (results.Total is 0)
            return 0;

        await RemoveAsync(results.Documents);
        return results.Total;
    }
}
