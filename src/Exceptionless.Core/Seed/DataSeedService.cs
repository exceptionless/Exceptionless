using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Seed;

public class DataSeedService : IStartupAction
{
    private readonly IEnumerable<IDataSeed> _seeds;
    private readonly MigrationManager _migrationManager;
    private readonly ILogger _logger;

    public DataSeedService(IEnumerable<IDataSeed> seeds, MigrationManager migrationManager, ILoggerFactory loggerFactory)
    {
        _seeds = seeds;
        _migrationManager = migrationManager;
        _logger = loggerFactory.CreateLogger<DataSeedService>();
    }

    public async Task RunAsync(CancellationToken shutdownToken = default)
    {
        if (_migrationManager.Migrations.Count == 0)
            _migrationManager.AddMigrationsFromLoadedAssemblies();

        var migrationStatus = await _migrationManager.GetMigrationStatus();
        int pendingVersionedMigrationCount = migrationStatus.PendingMigrations.Count(migration => migration.Migration.MigrationType != MigrationType.Repeatable);
        if (pendingVersionedMigrationCount > 0)
        {
            _logger.LogInformation("Skipping data seeds while {PendingMigrationCount} versioned migration(s) are pending", pendingVersionedMigrationCount);
            return;
        }

        await SeedAsync(shutdownToken);
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var seed in _seeds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Running data seed {DataSeedName}", seed.Name);
            await seed.SeedAsync(cancellationToken);
        }
    }
}
