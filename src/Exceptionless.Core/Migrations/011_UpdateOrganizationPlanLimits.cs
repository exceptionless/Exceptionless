using Exceptionless.Core.Models;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

/// <summary>
/// Maps the creator field and refreshes stored user/project limits from each organization's current plan.
/// Existing organizations without a recorded creator retain membership-based eligibility.
/// </summary>
public sealed class UpdateOrganizationPlanLimits : MigrationBase
{
    private const int BatchSize = 100;
    private readonly ExceptionlessElasticConfiguration _configuration;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly BillingPlans _plans;

    public UpdateOrganizationPlanLimits(IOrganizationRepository organizationRepository, ExceptionlessElasticConfiguration configuration, BillingPlans plans, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _configuration = configuration;
        _plans = plans;

        MigrationType = MigrationType.VersionedAndResumable;
        Version = 11;
    }

    public override async Task RunAsync(MigrationContext context)
    {
        var mappingResponse = await _configuration.Client.Indices.PutMappingAsync<Organization>(mapping => mapping
            .Indices(_configuration.Organizations.VersionedName)
            .Properties(properties => properties.Keyword(organization => organization.CreatedByUserId)), context.CancellationToken);
        _logger.LogRequest(mappingResponse);
        if (!mappingResponse.IsValidResponse)
            throw new InvalidOperationException("Unable to map the organization created by user id.");

        var organizations = await _organizationRepository.FindAsync(
            query => query.SortAscending(organization => organization.Id),
            options => options
                .SoftDeleteMode(SoftDeleteQueryMode.All)
                .SearchAfterPaging()
                .PageLimit(BatchSize));

        int migrated = 0;
        while (organizations.Documents.Count is > 0)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            // Resolve the current plan inside the update, so a concurrent billing change isn't overwritten.
            var limits = _plans.Plans.ToDictionary(plan => plan.Id,
                plan => new { max_users = plan.MaxUsers, max_projects = plan.MaxProjects });
            var patch = new ScriptPatch("""
                if (params.limits.containsKey(ctx._source.plan_id)) {
                    def limits = params.limits[ctx._source.plan_id];
                    ctx._source.max_users = limits.max_users;
                    ctx._source.max_projects = limits.max_projects;
                }
                """)
            {
                Params = new Dictionary<string, object> { ["limits"] = limits }
            };
            await _organizationRepository.PatchAsync(
                organizations.Documents.Select(organization => organization.Id).ToArray(), patch,
                options => options.Cache().Notifications(false));
            migrated += organizations.Documents.Count;

            _logger.LogInformation("Refreshed plan limits for {OrganizationCount} organizations so far", migrated);
            if (!await organizations.NextPageAsync())
                break;
        }

        _logger.LogInformation("Refreshed plan limits for {OrganizationCount} organizations", migrated);
    }
}
