using System.Diagnostics;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport.Products.Elasticsearch;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Caching;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

public sealed class RepairVerifiedUserEmailVerification : MigrationBase
{
    private const string INVALID_VERIFIED_USER_DESCRIPTION = "is_email_address_verified=true and either a verify email address token or a non-default token expiration is present";
    private const string REMOVE_VERIFICATION_FIELDS_SCRIPT = "ctx._source.remove('verify_email_address_token'); ctx._source.remove('verify_email_address_token_expiration');";

    private readonly ICacheClient _cache;
    private readonly ElasticsearchClient _client;
    private readonly ExceptionlessElasticConfiguration _configuration;

    public RepairVerifiedUserEmailVerification(ExceptionlessElasticConfiguration configuration, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _cache = configuration.Cache;
        _client = configuration.Client;
        _configuration = configuration;

        MigrationType = MigrationType.VersionedAndResumable;
        Version = 3;
    }

    public override async Task RunAsync(MigrationContext context)
    {
        string index = _configuration.Users.VersionedName;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting verified user email verification repair migration. Version={MigrationVersion} Index={Index} Query={Query}",
            Version,
            index,
            INVALID_VERIFIED_USER_DESCRIPTION);

        _logger.LogInformation("Refreshing user index {Index} before counting invalid verified user records", index);
        var refreshResponse = await _client.Indices.RefreshAsync(index, context.CancellationToken);
        _logger.LogRequest(refreshResponse, LogLevel.Information);
        EnsureValidResponse(refreshResponse, "refresh the user index before migration");

        long recordsToRepair = await CountInvalidVerifiedUsersAsync(index, context.CancellationToken);
        _logger.LogInformation(
            "Found {RecordsToRepair:N0} verified user record(s) with stale email verification fields in index {Index}",
            recordsToRepair,
            index);

        if (recordsToRepair is 0)
        {
            _logger.LogInformation(
                "Verified user email verification repair migration completed without changes. Version={MigrationVersion} Index={Index} Duration={Duration}",
                Version,
                index,
                stopwatch.Elapsed);
            return;
        }

        _logger.LogWarning(
            "Repairing {RecordsToRepair:N0} verified user record(s) by removing verify_email_address_token and verify_email_address_token_expiration. Verified status and all unrelated fields will be preserved",
            recordsToRepair);

        var updateResponse = await _client.UpdateByQueryAsync<User>(
            index,
            request => request
                .Query(CreateInvalidVerifiedUserQuery())
                .Script(script => script.Source(REMOVE_VERIFICATION_FIELDS_SCRIPT).Lang(ScriptLanguage.Painless))
                .Conflicts(Conflicts.Abort)
                .Refresh(true),
            context.CancellationToken);

        _logger.LogRequest(updateResponse, LogLevel.Information);
        EnsureValidResponse(updateResponse, "repair invalid verified user records");

        int failureCount = updateResponse.Failures?.Count ?? 0;
        _logger.LogInformation(
            "Verified user repair update completed. Index={Index} Matched={Matched:N0} Updated={Updated:N0} Noops={Noops:N0} VersionConflicts={VersionConflicts:N0} Failures={Failures:N0} TimedOut={TimedOut} Duration={Duration}",
            index,
            updateResponse.Total,
            updateResponse.Updated,
            updateResponse.Noops,
            updateResponse.VersionConflicts,
            failureCount,
            updateResponse.TimedOut,
            stopwatch.Elapsed);

        if (updateResponse.TimedOut is true || failureCount > 0 || updateResponse.VersionConflicts > 0)
        {
            throw new InvalidOperationException(
                $"Verified user email verification repair did not complete cleanly. TimedOut={updateResponse.TimedOut}, Failures={failureCount}, VersionConflicts={updateResponse.VersionConflicts}.");
        }

        long recordsRemaining = await CountInvalidVerifiedUsersAsync(index, context.CancellationToken);
        _logger.LogInformation(
            "Post-migration verification found {RecordsRemaining:N0} invalid verified user record(s) remaining in index {Index}",
            recordsRemaining,
            index);

        if (recordsRemaining > 0)
            throw new InvalidOperationException($"Verified user email verification repair left {recordsRemaining:N0} invalid record(s) in index {index}.");

        _logger.LogInformation(
            "Invalidating repaired user caches. UserCachePrefix={UserCachePrefix} EmailCachePrefix={EmailCachePrefix}",
            nameof(User),
            UserRepository.EmailCacheKeyPrefix);
        await Task.WhenAll(
            _cache.RemoveByPrefixAsync(nameof(User)),
            _cache.RemoveByPrefixAsync(UserRepository.EmailCacheKeyPrefix));

        _logger.LogInformation(
            "Verified user email verification repair migration completed successfully. Version={MigrationVersion} Index={Index} Repaired={RecordsRepaired:N0} Duration={Duration}",
            Version,
            index,
            updateResponse.Updated,
            stopwatch.Elapsed);
    }

    private async Task<long> CountInvalidVerifiedUsersAsync(string index, CancellationToken cancellationToken)
    {
        var response = await _client.CountAsync<User>(
            index,
            request => request.Query(CreateInvalidVerifiedUserQuery()),
            cancellationToken);

        _logger.LogRequest(response, LogLevel.Information);
        EnsureValidResponse(response, "count invalid verified user records");
        return response.Count;
    }

    private static Query CreateInvalidVerifiedUserQuery()
    {
        return new BoolQuery
        {
            Filter =
            [
                new TermQuery
                {
                    Field = "is_email_address_verified",
                    Value = true
                }
            ],
            Should =
            [
                new ExistsQuery { Field = "verify_email_address_token" },
                new DateRangeQuery
                {
                    Field = "verify_email_address_token_expiration",
                    Gt = DateTime.MinValue.ToString("O")
                }
            ],
            MinimumShouldMatch = 1
        };
    }

    private static void EnsureValidResponse(ElasticsearchResponse response, string operation)
    {
        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Unable to {operation}: {response.DebugInformation}");
    }
}
