using System.Diagnostics;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

public sealed class PopulateOAuthApplicationOrganizationIds : MigrationBase
{
    private const int BatchSize = 500;
    private readonly IOAuthApplicationRepository _oauthApplicationRepository;
    private readonly IOAuthTokenRepository _oauthTokenRepository;

    public PopulateOAuthApplicationOrganizationIds(
        IOAuthApplicationRepository oauthApplicationRepository,
        IOAuthTokenRepository oauthTokenRepository,
        ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _oauthApplicationRepository = oauthApplicationRepository;
        _oauthTokenRepository = oauthTokenRepository;

        MigrationType = MigrationType.VersionedAndResumable;
        Version = 8;
    }

    public override async Task RunAsync(MigrationContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var tokens = await _oauthTokenRepository.FindAsync(
            query => query
                .SortAscending(token => token.ClientId)
                .SortAscending(token => token.Id),
            options => options.SearchAfterPaging().PageLimit(BatchSize));

        int processedTokens = 0;
        long updatedApplications = 0;
        do
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            foreach (var clientTokens in tokens.Documents.GroupBy(token => token.ClientId, StringComparer.Ordinal))
            {
                string[] organizationIds = clientTokens
                    .SelectMany(token => token.OrganizationIds)
                    .Where(id => !String.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                updatedApplications += await _oauthApplicationRepository.AddOrganizationIdsAsync(
                    clientTokens.Key,
                    organizationIds,
                    options => options.ImmediateConsistency().Notifications(false));
            }

            processedTokens += tokens.Documents.Count;
            await context.Lock.RenewAsync();
        } while (!context.CancellationToken.IsCancellationRequested && await tokens.NextPageAsync());

        context.CancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "Populated OAuth application organization identifiers. ProcessedTokens={ProcessedTokens:N0} UpdatedApplications={UpdatedApplications:N0} Duration={Duration}",
            processedTokens,
            updatedApplications,
            stopwatch.Elapsed);
    }
}
