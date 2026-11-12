using Elastic.Clients.Elasticsearch.Mapping;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Tests.Utility;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class UpdateOrganizationPlanLimitsMigrationTests : IntegrationTestsBase
{
    private readonly IOrganizationRepository _organizationRepository;

    public UpdateOrganizationPlanLimitsMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _organizationRepository = GetService<IOrganizationRepository>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<UpdateOrganizationPlanLimits>();
        services.AddSingleton<ILock>(EmptyLock.Empty);
        base.RegisterServices(services);
    }

    [Fact]
    public async Task RunAsync_ExistingOrganizations_UpdatesAllPagesAndCacheWithoutRewritingUnchangedPlans()
    {
        // Arrange
        var limited = new Organization { Name = "Existing Free Organization", PlanId = "EX_FREE", PlanName = "Free", PlanDescription = "Free", MaxUsers = -1, MaxProjects = -1, BillingChangedByUserId = TestConstants.UserId };
        var deleted = new Organization { Name = "Deleted Small Organization", PlanId = "EX_SMALL", PlanName = "Small", PlanDescription = "Small", MaxUsers = 10, MaxProjects = 5, IsDeleted = true };
        var custom = new Organization { Name = "Custom limits", PlanId = "CUSTOM", PlanName = "Custom", PlanDescription = "Custom", MaxUsers = 17, MaxProjects = 9 };
        var unchanged = new Organization { Name = "Current Free limits", PlanId = "EX_FREE", PlanName = "Free", PlanDescription = "Free", MaxUsers = 1, MaxProjects = 1 };
        var otherOrganizations = Enumerable.Range(0, 97).Select(index => new Organization
        {
            Name = $"Existing Free Organization {index}", PlanId = "EX_FREE", PlanName = "Free", PlanDescription = "Free", MaxUsers = -1, MaxProjects = -1
        }).ToArray();
        await _organizationRepository.AddAsync([limited, deleted, custom, unchanged, .. otherOrganizations], options => options.ImmediateConsistency());
        await _organizationRepository.GetByIdAsync(limited.Id, options => options.Cache());

        var configuration = GetService<ExceptionlessElasticConfiguration>();
        string indexName = configuration.Organizations.VersionedName;
        var customBefore = await configuration.Client.GetAsync<Organization>(indexName, custom.Id, TestCancellationToken);
        var unchangedBefore = await configuration.Client.GetAsync<Organization>(indexName, unchanged.Id, TestCancellationToken);

        var migration = GetService<UpdateOrganizationPlanLimits>();
        var migrationLock = GetService<ILock>();

        // Act
        await migration.RunAsync(new MigrationContext(migrationLock, _logger, TestCancellationToken));
        await RefreshDataAsync();
        var limitedAfter = await configuration.Client.GetAsync<Organization>(indexName, limited.Id, TestCancellationToken);
        await migration.RunAsync(new MigrationContext(migrationLock, _logger, TestCancellationToken));

        // Assert
        await RefreshDataAsync();
        foreach (string id in new[] { limited.Id, deleted.Id })
        {
            var organization = await _organizationRepository.GetByIdAsync(id, options => options.SoftDeleteMode(SoftDeleteQueryMode.All));
            Assert.NotNull(organization);
            Assert.Equal(id == limited.Id ? 1 : -1, organization.MaxUsers);
            Assert.Equal(id == limited.Id ? 1 : -1, organization.MaxProjects);
        }

        var preservedCustom = await _organizationRepository.GetByIdAsync(custom.Id);
        Assert.Equal(17, preservedCustom?.MaxUsers);
        Assert.Equal(9, preservedCustom?.MaxProjects);
        var customAfter = await configuration.Client.GetAsync<Organization>(indexName, custom.Id, TestCancellationToken);
        var unchangedAfter = await configuration.Client.GetAsync<Organization>(indexName, unchanged.Id, TestCancellationToken);
        var limitedAfterRerun = await configuration.Client.GetAsync<Organization>(indexName, limited.Id, TestCancellationToken);
        Assert.All(new[] { customBefore, customAfter, unchangedBefore, unchangedAfter, limitedAfter, limitedAfterRerun }, response =>
        {
            Assert.True(response.IsValidResponse, response.DebugInformation);
            Assert.True(response.Found);
        });
        Assert.Equal(customBefore.SeqNo, customAfter.SeqNo);
        Assert.Equal(unchangedBefore.SeqNo, unchangedAfter.SeqNo);
        Assert.Equal(limitedAfter.SeqNo, limitedAfterRerun.SeqNo);

        var cached = await _organizationRepository.GetByIdAsync(limited.Id, options => options.Cache());
        Assert.Equal(1, cached?.MaxUsers);
        Assert.Equal(1, cached?.MaxProjects);
        var migratedOrganizations = await _organizationRepository.GetByIdsAsync(otherOrganizations.Select(organization => organization.Id).ToArray());
        Assert.Equal(97, migratedOrganizations.Count);
        Assert.All(migratedOrganizations, organization =>
        {
            Assert.Equal(1, organization.MaxUsers);
            Assert.Equal(1, organization.MaxProjects);
        });

        // The last user to change billing isn't necessarily the creator, so legacy organizations stay ownerless.
        var withoutRecordedCreator = await _organizationRepository.GetByIdAsync(limited.Id);
        Assert.NotNull(withoutRecordedCreator);
        Assert.Null(withoutRecordedCreator.CreatedByUserId);
        Assert.Equal("Existing Free Organization", withoutRecordedCreator.Name);
        Assert.Equal("EX_FREE", withoutRecordedCreator.PlanId);
        Assert.Equal(TestConstants.UserId, withoutRecordedCreator.BillingChangedByUserId);
        var created = await _organizationRepository.FindAsync(q => q.FieldEquals(o => o.CreatedByUserId, TestConstants.UserId));
        Assert.Empty(created.Documents);
    }

    [Theory]
    [InlineData("EX_FREE", "EX_MEDIUM", -1)]
    [InlineData("EX_MEDIUM", "EX_FREE", 1)]
    public async Task RunAsync_ConcurrentBillingAndUsageChanges_PreservesChangesAndInvalidatesCache(string initialPlanId, string currentPlanId, int expectedLimit)
    {
        // Arrange
        var organization = new Organization
        {
            Name = "Migrating Organization",
            PlanId = initialPlanId,
            PlanName = "Free",
            PlanDescription = "Free",
            MaxUsers = 1,
            MaxProjects = 1
        };
        await _organizationRepository.AddAsync(organization, options => options.ImmediateConsistency());
        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var concurrentUsage = new AssistantUsageInfo
        {
            Date = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            PlanId = "EX_MEDIUM",
            PromptTokens = 1_000,
            CompletionTokens = 250,
            CostInMicrodollars = 250_000,
            LastUsedUtc = utcNow
        };
        bool billingChanged = false;
        using var afterQuery = _organizationRepository.AfterQuery.AddHandler(async (_, _) =>
        {
            if (billingChanged)
                return;

            billingChanged = true;
            await _organizationRepository.PatchAsync(organization.Id, new PartialPatch(new
            {
                plan_id = currentPlanId,
                billing_status = BillingStatus.Active,
                billing_price = 49m,
                stripe_subscription_id = "sub_concurrent",
                assistant_usage = new[] { concurrentUsage }
            }), command => command.ImmediateConsistency());
            await _organizationRepository.GetByIdAsync(organization.Id, command => command.Cache());
        });
        var migration = GetService<UpdateOrganizationPlanLimits>();
        var migrationLock = GetService<ILock>();

        // Act
        await migration.RunAsync(new MigrationContext(migrationLock, _logger, TestCancellationToken));
        await RefreshDataAsync();
        var persistedOrganization = await _organizationRepository.GetByIdAsync(organization.Id, options => options.Cache(false));
        var cachedOrganization = await _organizationRepository.GetByIdAsync(organization.Id, options => options.Cache());

        // Assert
        Assert.True(billingChanged);
        foreach (var migrated in new[] { persistedOrganization, cachedOrganization })
        {
            Assert.NotNull(migrated);
            Assert.Equal(expectedLimit, migrated.MaxUsers);
            Assert.Equal(expectedLimit, migrated.MaxProjects);
            Assert.Equal(currentPlanId, migrated.PlanId);
            Assert.Equal(BillingStatus.Active, migrated.BillingStatus);
            Assert.Equal(49m, migrated.BillingPrice);
            Assert.Equal("sub_concurrent", migrated.StripeSubscriptionId);
            Assert.Equal(concurrentUsage, Assert.Single(migrated.AssistantUsage));
        }
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

        var migration = GetService<UpdateOrganizationPlanLimits>();
        var migrationLock = GetService<ILock>();
        var context = new MigrationContext(migrationLock, _logger, TestCancellationToken);

        // Act
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
