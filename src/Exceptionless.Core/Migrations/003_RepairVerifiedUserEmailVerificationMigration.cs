using System.Diagnostics;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

public sealed class RepairVerifiedUserEmailVerificationMigration : MigrationBase
{
    private readonly ICacheClient _cache;
    private readonly IUserRepository _userRepository;

    public RepairVerifiedUserEmailVerificationMigration(
        ICacheClient cache,
        IUserRepository userRepository,
        ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _cache = cache;
        _userRepository = userRepository;

        MigrationType = MigrationType.VersionedAndResumable;
        Version = 3;
    }

    public override async Task RunAsync(MigrationContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting verified user email verification repair migration. Version={MigrationVersion}",
            Version);

        long recordsToRepair = await CountInvalidVerifiedUsersAsync();
        _logger.LogInformation(
            "Found {RecordsToRepair:N0} verified user record(s) with stale email verification fields",
            recordsToRepair);

        long repairedRecords = 0;
        if (recordsToRepair > 0)
        {
            _logger.LogWarning(
                "Repairing {RecordsToRepair:N0} verified user record(s) by clearing their stale email verification credentials",
                recordsToRepair);

            repairedRecords = await _userRepository.PatchAllAsync(
                AddInvalidVerifiedUserFilter,
                new ActionPatch<User>(user => user.MarkEmailAddressVerified()),
                options => options.Notifications(false));

            _logger.LogInformation(
                "Verified user repair update completed. Requested={RequestedRecords:N0} Repaired={RepairedRecords:N0} Duration={Duration}",
                recordsToRepair,
                repairedRecords,
                stopwatch.Elapsed);
        }

        _logger.LogInformation(
            "Invalidating user caches after verified user repair. UserCachePrefix={UserCachePrefix} EmailCachePrefix={EmailCachePrefix}",
            nameof(User),
            UserRepository.EmailCacheKeyPrefix);
        await Task.WhenAll(
            _cache.RemoveByPrefixAsync(nameof(User)),
            _cache.RemoveByPrefixAsync(UserRepository.EmailCacheKeyPrefix));

        long recordsRemaining = await CountInvalidVerifiedUsersAsync();
        _logger.LogInformation(
            "Post-migration verification found {RecordsRemaining:N0} invalid verified user record(s) remaining",
            recordsRemaining);

        if (recordsRemaining > 0)
            throw new InvalidOperationException($"Verified user email verification repair left {recordsRemaining:N0} invalid record(s).");

        _logger.LogInformation(
            "Verified user email verification repair migration completed successfully. Version={MigrationVersion} Repaired={RecordsRepaired:N0} Duration={Duration}",
            Version,
            repairedRecords,
            stopwatch.Elapsed);
    }

    private async Task<long> CountInvalidVerifiedUsersAsync()
    {
        var result = await _userRepository.CountAsync(AddInvalidVerifiedUserFilter);
        return result.Total;
    }

    private static IRepositoryQuery<User> AddInvalidVerifiedUserFilter(IRepositoryQuery<User> query)
    {
        return query
            .FieldEquals(user => user.IsEmailAddressVerified, true)
            .FieldOr(group => group
                .FieldNotEquals(user => user.VerifyEmailAddressToken, null!)
                .FieldGreaterThan(user => user.VerifyEmailAddressTokenExpiration, DateTime.MinValue));
    }
}
