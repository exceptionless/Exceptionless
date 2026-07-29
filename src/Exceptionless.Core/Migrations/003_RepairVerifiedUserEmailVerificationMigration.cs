using System.Diagnostics;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

public sealed class RepairVerifiedUserEmailVerificationMigration : MigrationBase
{
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
        var users = await _userRepository.GetVerifiedUsersWithStaleVerificationDataAsync(
            options => options.SearchAfterPaging().PageLimit(100));

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

            foreach (var user in users.Documents)
                user.MarkEmailAddressVerified();

            await _userRepository.SaveAsync(users.Documents);
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
