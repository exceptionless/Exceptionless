using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

/// <summary>
/// Prepares organizations for free trials: maps <see cref="Organization.CreatedByUserId"/> so ownership can be
/// queried and backfills it for existing organizations from the user who last changed the plan (the creator for
/// free organizations), and, because plans no longer limit users or projects, sets the stored limits to unlimited.
/// </summary>
public sealed class UpdateOrganizationsForTrials : MigrationBase
{
    private const int BatchSize = 100;
    private readonly ExceptionlessElasticConfiguration _configuration;
    private readonly IOrganizationRepository _organizationRepository;

    public UpdateOrganizationsForTrials(IOrganizationRepository organizationRepository, ExceptionlessElasticConfiguration configuration, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _configuration = configuration;

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
        while (organizations.Documents.Count > 0)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var organizationsToMigrate = organizations.Documents
                .Where(organization => organization.MaxUsers != -1 || organization.MaxProjects != -1
                    || (organization.CreatedByUserId is null && organization.BillingChangedByUserId is not null))
                .ToList();
            if (organizationsToMigrate.Count > 0)
            {
                foreach (var organization in organizationsToMigrate)
                {
                    organization.MaxUsers = -1;
                    organization.MaxProjects = -1;
                    organization.CreatedByUserId ??= organization.BillingChangedByUserId;
                }

                await _organizationRepository.SaveAsync(organizationsToMigrate, options => options.Cache().Notifications(false));
                migrated += organizationsToMigrate.Count;
            }

            if (!await organizations.NextPageAsync())
                break;
        }

        _logger.LogInformation("Updated {OrganizationCount} organizations for free trials", migrated);
    }
}
