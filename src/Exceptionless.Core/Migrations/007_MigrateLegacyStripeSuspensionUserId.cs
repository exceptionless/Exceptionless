using Elastic.Clients.Elasticsearch;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

public sealed class MigrateLegacyStripeSuspensionUserId : MigrationBase
{
    private const int BatchSize = 100;
    private readonly ExceptionlessElasticConfiguration _configuration;
    private readonly IOrganizationRepository _organizationRepository;

    public MigrateLegacyStripeSuspensionUserId(
        IOrganizationRepository organizationRepository,
        ExceptionlessElasticConfiguration configuration,
        ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _configuration = configuration;

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

            // SuspendedByUserId is not mapped on OrganizationIndex, so the legacy marker must be
            // filtered after the typed repository query loads the current document source.
            var organizationsToMigrate = organizations.Documents
                .Where(organization => String.Equals(organization.SuspendedByUserId, StripeConstants.LegacySystemUserId, StringComparison.Ordinal))
                .ToList();
            if (organizationsToMigrate.Count > 0)
            {
                organizationsToMigrate.ForEach(organization => organization.SuspendedByUserId = StripeConstants.SystemUserId);
                await _organizationRepository.SaveAsync(organizationsToMigrate, options => options.Cache());
                migrated += organizationsToMigrate.Count;
            }

            if (!await organizations.NextPageAsync())
                break;
        }

        if (migrated > 0)
        {
            var refreshResponse = await _configuration.Client.Indices.RefreshAsync(
                _configuration.Organizations.VersionedName,
                context.CancellationToken);
            _logger.LogRequest(refreshResponse);
            if (!refreshResponse.IsValidResponse)
                throw new InvalidOperationException("Unable to refresh organizations after the legacy Stripe suspension user id migration.");
        }

        _logger.LogInformation("Migrated {OrganizationCount} organizations with the legacy Stripe suspension user id", migrated);
    }
}
