using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
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
    public async Task RunAsync_LegacyOrganizations_BecomeUnlimitedAndGetQueryableCreator()
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

        var created = await _organizationRepository.FindAsync(q => q.FieldEquals(o => o.CreatedByUserId, TestConstants.UserId));
        Assert.Equal(limited.Id, Assert.Single(created.Documents).Id);
    }
}
