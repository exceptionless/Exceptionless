using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

public sealed class MigrateLegacyStripeSuspensionUserId : MigrationBase
{
    private const int BatchSize = 100;
    private readonly IOrganizationRepository _organizationRepository;

    public MigrateLegacyStripeSuspensionUserId(IOrganizationRepository organizationRepository, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;

        MigrationType = MigrationType.VersionedAndResumable;
        Version = 7;
    }

    public override async Task RunAsync(MigrationContext context)
    {
        var organizations = await _organizationRepository.FindAsync(
            query => query
                .FieldEquals(organization => organization.IsSuspended, true)
                .SortAscending(organization => organization.Id),
            options => options
                .SoftDeleteMode(SoftDeleteQueryMode.All)
                .SearchAfterPaging()
                .PageLimit(BatchSize));

        int migrated = 0;
        while (organizations.Documents.Count > 0)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            foreach (var organization in organizations.Documents)
            {
                if (!String.Equals(organization.SuspendedByUserId, StripeConstants.LegacySystemUserId, StringComparison.Ordinal))
                    continue;

                organization.SuspendedByUserId = StripeConstants.SystemUserId;
                await _organizationRepository.SaveAsync(organization, options => options.Cache());
                migrated++;
            }

            if (!await organizations.NextPageAsync())
                break;
        }

        _logger.LogInformation("Migrated {OrganizationCount} organizations with the legacy Stripe suspension user id", migrated);
    }
}
