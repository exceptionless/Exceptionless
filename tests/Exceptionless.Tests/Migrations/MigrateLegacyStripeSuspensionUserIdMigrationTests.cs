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
    private const string LegacySystemUserId = "Stripe";
    private const string ExpectedSystemUserId = "000000000000000000000000";
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
    public async Task RunAsync_ReplacesHistoricalStripeMarkerAndPreservesSuspensionState()
    {
        // Arrange
        var organization = new Organization
        {
            Name = "Legacy Stripe Suspension Organization",
            PlanId = _plans.FreePlan.Id,
            IsSuspended = true,
            SuspensionCode = SuspensionCode.Billing,
            SuspensionDate = DateTime.UtcNow,
            SuspensionNotes = "Stripe subscription deleted.",
            SuspendedByUserId = ValidSuspendedByUserId
        };
        organization = await _organizationRepository.AddAsync(organization, o => o.ImmediateConsistency());

        var legacyMarkerUpdate = await _configuration.Client.UpdateAsync<Organization, Dictionary<string, object>>(
            _configuration.Organizations.VersionedName,
            organization.Id,
            update => update
                .Doc(new Dictionary<string, object> { ["suspended_by_user_id"] = LegacySystemUserId })
                .Refresh(Refresh.WaitFor),
            TestCancellationToken);
        Assert.True(legacyMarkerUpdate.IsValidResponse);

        // Act
        var migration = GetService<MigrateLegacyStripeSuspensionUserId>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);
        await migration.RunAsync(context);

        // Assert
        var migratedOrganization = await _organizationRepository.GetByIdAsync(organization.Id);
        Assert.NotNull(migratedOrganization);
        Assert.True(migratedOrganization.IsSuspended);
        Assert.Equal(SuspensionCode.Billing, migratedOrganization.SuspensionCode);
        Assert.Equal(organization.SuspensionDate, migratedOrganization.SuspensionDate);
        Assert.Equal(organization.SuspensionNotes, migratedOrganization.SuspensionNotes);
        Assert.Equal(ExpectedSystemUserId, migratedOrganization.SuspendedByUserId);
    }
}
