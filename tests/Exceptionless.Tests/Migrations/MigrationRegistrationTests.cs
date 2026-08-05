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
}
