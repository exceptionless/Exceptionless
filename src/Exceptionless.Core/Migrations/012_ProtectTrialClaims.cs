using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Repositories;
using Foundatio.Repositories.Exceptions;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

/// <summary>
/// Replaces unkeyed mailbox digests with HMAC identifiers without needing the original email addresses.
/// Creates the protected claim before removing the old one, so interruption cannot erase trial history.
/// </summary>
public sealed class ProtectTrialClaims : MigrationBase
{
    private readonly ITrialClaimRepository _repository;
    private readonly ExceptionlessElasticConfiguration _configuration;
    private readonly AppOptions _options;

    public ProtectTrialClaims(ITrialClaimRepository repository, ExceptionlessElasticConfiguration configuration, AppOptions options, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _repository = repository;
        _configuration = configuration;
        _options = options;
        Version = 12;
        MigrationType = MigrationType.VersionedAndResumable;
    }

    public override async Task RunAsync(MigrationContext context)
    {
        var exists = await _configuration.Client.Indices.ExistsAsync(_configuration.TrialClaims.VersionedName, context.CancellationToken);
        if (!exists.IsValidResponse && exists.ApiCallDetails.HttpStatusCode is not 404)
            throw new InvalidOperationException("Unable to inspect the trial claims index");
        if (!exists.Exists)
            return;

        var page = await _repository.GetAllAsync(command => command.SearchAfterPaging().PageLimit(100));
        int migrated = 0;
        bool hasNextPage;
        do
        {
            foreach (var claim in page.Documents)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (claim.Id.StartsWith("hmac-v1-", StringComparison.Ordinal))
                    continue;

                string secret = _options.BillingOptions.TrialClaimSecret
                    ?? throw new InvalidOperationException("Billing:TrialClaimSecret is required to protect existing trial claims");
                string protectedId = TrialEmailAddress.ProtectLegacyClaimId(claim.Id, secret);
                try
                {
                    await _repository.AddAsync(new TrialClaim { Id = protectedId, CreatedUtc = claim.CreatedUtc }, command => command.Notifications(false));
                }
                catch (DuplicateDocumentException)
                {
                    // A prior attempt already persisted this claim; finish removing its old unkeyed identifier.
                }

                await _repository.RemoveAsync(claim.Id, command => command.Notifications(false));
                migrated++;
            }

            _logger.LogInformation("Protected {ClaimCount} legacy trial claims", migrated);
            hasNextPage = await page.NextPageAsync();
        } while (hasNextPage);
    }
}
