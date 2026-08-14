using Exceptionless.Core.Migrations;
using Foundatio.Repositories.Migrations;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class MigrationRegistrationTests : TestWithServices
{
    public MigrationRegistrationTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void RegisteredVersionedMigrations_AllVersions_AreUnique()
    {
        var duplicateVersions = GetService<IEnumerable<IMigration>>()
            .DistinctBy(migration => migration.GetType())
            .Where(migration => migration.MigrationType is MigrationType.Versioned or MigrationType.VersionedAndResumable)
            .GroupBy(migration => migration.Version)
            .Where(group => group.Count() > 1)
            .Select(group => new
            {
                Version = group.Key,
                Migrations = group.Select(migration => migration.GetType().Name).Order().ToArray()
            })
            .ToList();

        Assert.Empty(duplicateVersions);
    }

    [Fact]
    public void LegacyStripeSuspensionMigration_IsRegisteredAsVersionedAndResumable()
    {
        var migration = GetService<IEnumerable<IMigration>>()
            .DistinctBy(migration => migration.GetType())
            .SingleOrDefault(migration => migration is MigrateLegacyStripeSuspensionUserId);

        Assert.NotNull(migration);
        Assert.Equal(MigrationType.VersionedAndResumable, migration.MigrationType);
        Assert.Equal(7, migration.Version);
    }
}
