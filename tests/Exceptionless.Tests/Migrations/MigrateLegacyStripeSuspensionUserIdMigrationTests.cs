using Elastic.Clients.Elasticsearch;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Utility;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class MigrateLegacyStripeSuspensionUserIdMigrationTests : IntegrationTestsBase
{
    // This is the exact value written by StripeEventHandler before the system user id was introduced.
    private const string HistoricalStripeSuspensionUserId = "Stripe";
    private const string ValidSuspendedByUserId = "660000000000000000000001";
    private readonly ExceptionlessElasticConfiguration _configuration;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly BillingPlans _plans;

    public MigrateLegacyStripeSuspensionUserIdMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _configuration = GetService<ExceptionlessElasticConfiguration>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _plans = GetService<BillingPlans>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<MigrateLegacyStripeSuspensionUserId>();
        services.AddSingleton<ILock>(EmptyLock.Empty);
        base.RegisterServices(services);
    }

    [Fact]
    public async Task RunAsync_HistoricalStripeMarker_IsMigratedAndSuspensionStateIsPreserved()
    {
        // Arrange
        var organization = await AddSuspendedOrganizationAsync(ValidSuspendedByUserId, "Legacy Stripe Suspension Organization");
        await UpdateOrganizationDocumentAsync(organization.Id, new Dictionary<string, object>
        {
            ["suspended_by_user_id"] = HistoricalStripeSuspensionUserId
        });

        // Act
        await RunMigrationAsync();

        // Assert
        var migratedOrganization = await GetOrganizationAsync(organization.Id);
        Assert.NotNull(migratedOrganization);
        Assert.Equal(organization.Name, migratedOrganization.Name);
        Assert.Equal(organization.PlanId, migratedOrganization.PlanId);
        Assert.True(migratedOrganization.IsSuspended);
        Assert.False(migratedOrganization.IsDeleted);
        Assert.Equal(SuspensionCode.Billing, migratedOrganization.SuspensionCode);
        Assert.Equal(organization.SuspensionDate, migratedOrganization.SuspensionDate);
        Assert.Equal(organization.SuspensionNotes, migratedOrganization.SuspensionNotes);
        Assert.Equal(StripeConstants.SystemUserId, migratedOrganization.SuspendedByUserId);
    }

    [Fact]
    public async Task RunAsync_SuspendedOrganizationWithNonLegacyMarker_AndUnsuspendedLegacyRecord_AreUnchanged()
    {
        // Arrange
        var organizationWithCurrentUser = await AddSuspendedOrganizationAsync(ValidSuspendedByUserId, "Current Suspension Organization");
        var unsuspendedOrganization = await _organizationRepository.AddAsync(new Organization
        {
            Name = "Unsuspended Legacy Organization",
            PlanId = _plans.FreePlan.Id
        }, o => o.ImmediateConsistency());
        await UpdateOrganizationDocumentAsync(unsuspendedOrganization.Id, new Dictionary<string, object>
        {
            ["is_suspended"] = false,
            ["suspended_by_user_id"] = HistoricalStripeSuspensionUserId
        });

        // Act
        await RunMigrationAsync();

        // Assert
        var unchangedSuspension = await GetOrganizationAsync(organizationWithCurrentUser.Id);
        Assert.NotNull(unchangedSuspension);
        Assert.True(unchangedSuspension.IsSuspended);
        Assert.Equal(ValidSuspendedByUserId, unchangedSuspension.SuspendedByUserId);

        var unchangedUnsuspendedOrganization = await GetOrganizationAsync(unsuspendedOrganization.Id);
        Assert.NotNull(unchangedUnsuspendedOrganization);
        Assert.False(unchangedUnsuspendedOrganization.IsSuspended);
        Assert.Equal(HistoricalStripeSuspensionUserId, unchangedUnsuspendedOrganization.SuspendedByUserId);
    }

    [Fact]
    public async Task RunAsync_SoftDeletedSuspendedOrganization_MigratesAndRemainsDeleted()
    {
        // Arrange
        var organization = await AddSuspendedOrganizationAsync(ValidSuspendedByUserId, "Deleted Legacy Stripe Organization");
        organization.IsDeleted = true;
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
        await UpdateOrganizationDocumentAsync(organization.Id, new Dictionary<string, object>
        {
            ["suspended_by_user_id"] = HistoricalStripeSuspensionUserId
        });

        // Act
        await RunMigrationAsync();

        // Assert
        var migratedOrganization = await GetOrganizationAsync(organization.Id, includeSoftDeletes: true);
        Assert.NotNull(migratedOrganization);
        Assert.True(migratedOrganization.IsDeleted);
        Assert.True(migratedOrganization.IsSuspended);
        Assert.Equal(StripeConstants.SystemUserId, migratedOrganization.SuspendedByUserId);
    }

    [Fact]
    public async Task RunAsync_WhenRerun_DoesNotChangeAlreadyMigratedRecord()
    {
        // Arrange
        var organization = await AddSuspendedOrganizationAsync(ValidSuspendedByUserId, "Idempotent Legacy Stripe Organization");
        await UpdateOrganizationDocumentAsync(organization.Id, new Dictionary<string, object>
        {
            ["suspended_by_user_id"] = HistoricalStripeSuspensionUserId
        });

        // Act
        await RunMigrationAsync();
        var migratedOrganization = await GetOrganizationAsync(organization.Id);
        Assert.NotNull(migratedOrganization);

        await RunMigrationAsync();

        // Assert
        var rerunOrganization = await GetOrganizationAsync(organization.Id);
        Assert.NotNull(rerunOrganization);
        Assert.Equal(migratedOrganization.UpdatedUtc, rerunOrganization.UpdatedUtc);
        Assert.Equal(StripeConstants.SystemUserId, rerunOrganization.SuspendedByUserId);
        Assert.Equal(migratedOrganization.SuspensionDate, rerunOrganization.SuspensionDate);
        Assert.Equal(migratedOrganization.SuspensionNotes, rerunOrganization.SuspensionNotes);
    }

    [Fact]
    public async Task RunAsync_MoreThanOnePageOfLegacyRecords_MigratesEveryRecord()
    {
        // Arrange
        var organizations = Enumerable.Range(0, 101)
            .Select(index => new Organization
            {
                Name = $"Legacy Stripe Page Organization {index}",
                PlanId = _plans.FreePlan.Id,
                IsSuspended = true,
                SuspensionCode = SuspensionCode.Billing,
                SuspensionDate = DateTime.UtcNow,
                SuspensionNotes = "Stripe subscription deleted.",
                SuspendedByUserId = ValidSuspendedByUserId
            })
            .ToList();
        await _organizationRepository.AddAsync(organizations, o => o.ImmediateConsistency());
        await SetLegacyMarkerOnSuspendedOrganizationsAsync();

        // Act
        await RunMigrationAsync();

        // Assert
        var migratedOrganizations = await _organizationRepository.FindAsync(
            query => query.FieldEquals(organization => organization.IsSuspended, true),
            options => options.PageLimit(organizations.Count));
        Assert.Equal(organizations.Count, migratedOrganizations.Documents.Count);
        Assert.All(migratedOrganizations.Documents, organization =>
            Assert.Equal(StripeConstants.SystemUserId, organization.SuspendedByUserId));
    }

    private Task<Organization> AddSuspendedOrganizationAsync(string suspendedByUserId, string name)
    {
        return _organizationRepository.AddAsync(new Organization
        {
            Name = name,
            PlanId = _plans.FreePlan.Id,
            IsSuspended = true,
            SuspensionCode = SuspensionCode.Billing,
            SuspensionDate = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            SuspensionNotes = "Stripe subscription deleted.",
            SuspendedByUserId = suspendedByUserId
        }, o => o.ImmediateConsistency());
    }

    private Task RunMigrationAsync()
    {
        var migration = GetService<MigrateLegacyStripeSuspensionUserId>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);
        return migration.RunAsync(context);
    }

    private Task<Organization?> GetOrganizationAsync(string id, bool includeSoftDeletes = false)
    {
        return _organizationRepository.GetByIdAsync(
            id,
            options => options.SoftDeleteMode(includeSoftDeletes ? SoftDeleteQueryMode.All : SoftDeleteQueryMode.ActiveOnly));
    }

    private async Task UpdateOrganizationDocumentAsync(string id, Dictionary<string, object> document)
    {
        var response = await _configuration.Client.UpdateAsync<Organization, Dictionary<string, object>>(
            _configuration.Organizations.VersionedName,
            id,
            update => update
                .Doc(document)
                .Refresh(Refresh.WaitFor),
            TestCancellationToken);
        Assert.True(response.IsValidResponse);
    }

    private async Task SetLegacyMarkerOnSuspendedOrganizationsAsync()
    {
        var response = await _configuration.Client.UpdateByQueryAsync<Organization>(update => update
            .Indices(_configuration.Organizations.VersionedName)
            .Query(query => query.QueryString(queryString => queryString.Query("is_suspended:true")))
            .Script(script => script
                .Source("ctx._source.suspended_by_user_id = 'Stripe';")
                .Lang(ScriptLanguage.Painless))
            .Conflicts(Conflicts.Proceed),
            TestCancellationToken);
        Assert.True(response.IsValidResponse);

        var refreshResponse = await _configuration.Client.Indices.RefreshAsync(
            _configuration.Organizations.VersionedName,
            TestCancellationToken);
        Assert.True(refreshResponse.IsValidResponse);
    }
}
