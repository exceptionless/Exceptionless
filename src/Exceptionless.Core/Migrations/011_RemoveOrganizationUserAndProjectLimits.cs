using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

/// <summary>
/// Plans no longer limit users or projects. Existing organizations still store their previous plan's limits,
/// so set them to unlimited to match the plans.
/// </summary>
public sealed class RemoveOrganizationUserAndProjectLimits : MigrationBase
{
    private const int BatchSize = 100;
    private readonly IOrganizationRepository _organizationRepository;

    public RemoveOrganizationUserAndProjectLimits(IOrganizationRepository organizationRepository, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;

        MigrationType = MigrationType.VersionedAndResumable;
        Version = 11;
    }

    public override async Task RunAsync(MigrationContext context)
    {
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
                .Where(organization => organization.MaxUsers != -1 || organization.MaxProjects != -1)
                .ToList();
            if (organizationsToMigrate.Count > 0)
            {
                foreach (var organization in organizationsToMigrate)
                {
                    organization.MaxUsers = -1;
                    organization.MaxProjects = -1;
                }

                await _organizationRepository.SaveAsync(organizationsToMigrate, options => options.Cache().Notifications(false));
                migrated += organizationsToMigrate.Count;
            }

            if (!await organizations.NextPageAsync())
                break;
        }

        _logger.LogInformation("Removed user and project limits from {OrganizationCount} organizations", migrated);
    }
}
