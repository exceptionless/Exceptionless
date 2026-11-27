using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Utility;
using Exceptionless.Tests.Utility;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class MoveProjectStackingSettingsToDataMigrationTests : IntegrationTestsBase
{
    private readonly IProjectRepository _projectRepository;
    private readonly MoveProjectStackingSettingsToData _migration;

    public MoveProjectStackingSettingsToDataMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _projectRepository = GetService<IProjectRepository>();
        _migration = new MoveProjectStackingSettingsToData(_projectRepository, GetService<ILoggerFactory>());
    }

    [Fact]
    public async Task RunAsync_ConfigurationSettings_MovesValuesToProjectDataAndCanRunAgain()
    {
        // Arrange
        var both = NewProject("Both");
        both.Configuration.Settings["UserNamespaces"] = "Contoso.App";
        both.Configuration.Settings["CommonMethods"] = "Assert, Writeline";
        both.Configuration.Settings["Other"] = "kept";
        both.Data!["UserNamespaces"] = "Old.Value";
        both.Data["Untouched"] = "kept";

        var dataOnly = NewProject("DataOnly");
        dataOnly.Data!["CommonMethods"] = "FromData";

        var empty = NewProject("Empty");
        empty.Configuration.Settings["CommonMethods"] = "  ";

        var deleted = NewProject("Deleted");
        deleted.IsDeleted = true;
        deleted.Configuration.Settings["UserNamespaces"] = "Deleted.App";

        var otherProjects = Enumerable.Range(0, 120).Select(index => NewProject($"Other {index}")).ToArray();
        otherProjects[119].Configuration.Settings["UserNamespaces"] = "Paged.App";
        await _projectRepository.AddAsync([both, dataOnly, empty, deleted, .. otherProjects], options => options.ImmediateConsistency());
        int versionBefore = both.Configuration.Version;

        // Act
        var context = new MigrationContext(EmptyLock.Empty, _logger, TestCancellationToken);
        await _migration.RunAsync(context);
        await _migration.RunAsync(context);

        // Assert
        await RefreshDataAsync();
        var migrated = await _projectRepository.GetByIdAsync(both.Id, options => options.Cache());
        Assert.NotNull(migrated);
        Assert.Equal("Contoso.App", migrated.Data!["UserNamespaces"]);
        Assert.Equal("Assert, Writeline", migrated.Data["CommonMethods"]);
        Assert.Equal("kept", migrated.Data["Untouched"]);
        Assert.False(migrated.Configuration.Settings.ContainsKey("UserNamespaces"));
        Assert.False(migrated.Configuration.Settings.ContainsKey("CommonMethods"));
        Assert.Equal("kept", migrated.Configuration.Settings["Other"]);
        Assert.Equal(versionBefore + 1, migrated.Configuration.Version);

        var untouched = await _projectRepository.GetByIdAsync(dataOnly.Id);
        Assert.Equal("FromData", untouched!.Data!["CommonMethods"]);
        Assert.Equal(dataOnly.Configuration.Version, untouched.Configuration.Version);

        var emptied = await _projectRepository.GetByIdAsync(empty.Id);
        Assert.False(emptied!.Configuration.Settings.ContainsKey("CommonMethods"));
        Assert.False(emptied.Data!.ContainsKey("CommonMethods"));

        var deletedProject = await _projectRepository.GetByIdAsync(deleted.Id, options => options.IncludeSoftDeletes());
        Assert.Equal("Deleted.App", deletedProject!.Data!["UserNamespaces"]);
        Assert.False(deletedProject.Configuration.Settings.ContainsKey("UserNamespaces"));

        var paged = await _projectRepository.GetByIdAsync(otherProjects[119].Id);
        Assert.Equal("Paged.App", paged!.Data!["UserNamespaces"]);
    }

    private static Project NewProject(string name) => new()
    {
        OrganizationId = TestConstants.OrganizationId,
        Name = name,
        NextSummaryEndOfDayTicks = 1
    };
}
