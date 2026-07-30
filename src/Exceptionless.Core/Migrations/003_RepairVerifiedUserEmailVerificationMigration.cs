using System.Diagnostics;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

public sealed class RepairVerifiedUserEmailVerificationMigration : MigrationBase
{
    private const int BatchSize = 100;
    private readonly IUserRepository _userRepository;

    public RepairVerifiedUserEmailVerificationMigration(
        IUserRepository userRepository,
        ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
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

        long repairedRecords = 0;
        int batch = 0;
        var users = await _userRepository.FindAsync(
            query => query
                .FieldEquals(user => user.IsEmailAddressVerified, true)
                .FieldOr(group => group
                    .FieldNotEquals(user => user.VerifyEmailAddressToken, null!)
                    .FieldGreaterThan(user => user.VerifyEmailAddressTokenExpiration, DateTime.MinValue))
                .SortAscending(user => user.Id),
            options => options.SearchAfterPaging().PageLimit(BatchSize));

        if (users.Documents.Count == 0)
        {
            _logger.LogInformation(
                "Verified user email verification repair migration completed without changes. Version={MigrationVersion} Duration={Duration}",
                Version,
                stopwatch.Elapsed);
            return;
        }

        do
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            batch++;

            await _userRepository.PatchAsync(
                users.Documents.Select(user => user.Id).ToArray(),
                new ActionPatch<User>(user => user.MarkEmailAddressVerified()));
            repairedRecords += users.Documents.Count;

            _logger.LogInformation(
                "Repaired verified user email verification data. Batch={Batch:N0} BatchRepaired={BatchRepaired:N0} TotalRepaired={TotalRepaired:N0} Duration={Duration}",
                batch,
                users.Documents.Count,
                repairedRecords,
                stopwatch.Elapsed);
        } while (!context.CancellationToken.IsCancellationRequested && await users.NextPageAsync());

        context.CancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "Verified user email verification repair migration completed successfully. Version={MigrationVersion} Batches={Batches:N0} Repaired={RecordsRepaired:N0} Duration={Duration}",
            Version,
            batch,
            repairedRecords,
            stopwatch.Elapsed);
    }
}
