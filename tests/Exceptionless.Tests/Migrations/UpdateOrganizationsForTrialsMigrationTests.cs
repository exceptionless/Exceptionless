using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Tests.Utility;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class UpdateOrganizationsForTrialsMigrationTests : IntegrationTestsBase
{
    private readonly IOrganizationRepository _organizationRepository;

    public UpdateOrganizationsForTrialsMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _organizationRepository = GetService<IOrganizationRepository>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<UpdateOrganizationsForTrials>();
        services.AddSingleton<ILock>(EmptyLock.Empty);
        base.RegisterServices(services);
    }

    [Fact]
    public async Task RunAsync_LegacyOrganizations_BecomeUnlimitedWithoutGuessingCreator()
    {
        // Arrange
        var limited = new Organization { Name = "Legacy Free Organization", PlanId = "EX_FREE", PlanName = "Free", PlanDescription = "Free", MaxUsers = 1, MaxProjects = 1, BillingChangedByUserId = TestConstants.UserId };
        var deleted = new Organization { Name = "Deleted Small Organization", PlanId = "EX_FREE", PlanName = "Free", PlanDescription = "Free", MaxUsers = 10, MaxProjects = 5, IsDeleted = true };
        await _organizationRepository.AddAsync([limited, deleted], options => options.ImmediateConsistency());

        // Act
        var migration = GetService<UpdateOrganizationsForTrials>();
        await migration.RunAsync(new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken));

        // Assert
        await RefreshDataAsync();
        foreach (string id in new[] { limited.Id, deleted.Id })
        {
            var organization = await _organizationRepository.GetByIdAsync(id, options => options.SoftDeleteMode(SoftDeleteQueryMode.All));
            Assert.NotNull(organization);
            Assert.Equal(-1, organization.MaxUsers);
            Assert.Equal(-1, organization.MaxProjects);
        }

        // The last user to change billing isn't necessarily the creator, so legacy organizations stay ownerless.
        var legacy = await _organizationRepository.GetByIdAsync(limited.Id);
        Assert.NotNull(legacy);
        Assert.Null(legacy.CreatedByUserId);
        Assert.Equal("Legacy Free Organization", legacy.Name);
        Assert.Equal("EX_FREE", legacy.PlanId);
        Assert.Equal(TestConstants.UserId, legacy.BillingChangedByUserId);
        var created = await _organizationRepository.FindAsync(q => q.FieldEquals(o => o.CreatedByUserId, TestConstants.UserId));
        Assert.Empty(created.Documents);
    }

    [Fact]
    public async Task RunAsync_ExistingOrganizationIndex_AddsCreatorMappingAndKeepsEverythingElse()
    {
        // Arrange
        var configuration = GetService<ExceptionlessElasticConfiguration>();
        string indexName = configuration.Organizations.VersionedName;
        var mappingBefore = await configuration.Client.Indices.GetMappingAsync(indexName, TestCancellationToken);
        Assert.True(mappingBefore.IsValidResponse, mappingBefore.DebugInformation);
        var propertiesBefore = Assert.Single(mappingBefore.Mappings).Value.Mappings.Properties!;
        var settingsBefore = await configuration.Client.Indices.GetSettingsAsync((Elastic.Clients.Elasticsearch.Indices)indexName, TestCancellationToken);
        Assert.True(settingsBefore.IsValidResponse, settingsBefore.DebugInformation);
        var indexSettingsBefore = Assert.Single(settingsBefore.Settings).Value.Settings!;

        // Act
        var migration = GetService<UpdateOrganizationsForTrials>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);
        await migration.RunAsync(context);
        await migration.RunAsync(context);

        // Assert
        var mappingAfter = await configuration.Client.Indices.GetMappingAsync(indexName, TestCancellationToken);
        Assert.True(mappingAfter.IsValidResponse, mappingAfter.DebugInformation);
        var propertiesAfter = Assert.Single(mappingAfter.Mappings).Value.Mappings.Properties!;
        Assert.IsType<KeywordProperty>(propertiesAfter["created_by_user_id"]);
        foreach (var (name, property) in propertiesBefore)
        {
            Assert.True(propertiesAfter.TryGetProperty(name, out var after), $"Mapping for {name} was removed.");
            Assert.Equal(property.Type, after!.Type);
        }

        var settingsAfter = await configuration.Client.Indices.GetSettingsAsync((Elastic.Clients.Elasticsearch.Indices)indexName, TestCancellationToken);
        Assert.True(settingsAfter.IsValidResponse, settingsAfter.DebugInformation);
        var indexSettingsAfter = Assert.Single(settingsAfter.Settings).Value.Settings!;
        Assert.Equal(indexSettingsBefore.Index?.NumberOfShards?.ToString(), indexSettingsAfter.Index?.NumberOfShards?.ToString());
        Assert.Equal(indexSettingsBefore.Index?.NumberOfReplicas?.ToString(), indexSettingsAfter.Index?.NumberOfReplicas?.ToString());
        Assert.Equal(indexSettingsBefore.Index?.Analysis?.Analyzers?.Count(), indexSettingsAfter.Index?.Analysis?.Analyzers?.Count());
    }
}
