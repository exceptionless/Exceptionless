using Exceptionless.Core.Billing;
using Exceptionless.Core.Migrations;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Seed;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Utility;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class AddSessionsPredefinedSavedViewMigrationTests : IntegrationTestsBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISavedViewRepository _savedViewRepository;

    public AddSessionsPredefinedSavedViewMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _organizationRepository = GetService<IOrganizationRepository>();
        _savedViewRepository = GetService<ISavedViewRepository>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        services.AddTransient<AddSessionsPredefinedSavedView>();
        services.AddSingleton<ILock>(EmptyLock.Empty);
        base.RegisterServices(services);
    }

    [Fact]
    public async Task RunAsync_ExistingDefinitionsWithoutSessions_AddsSessionsDefinitionOnce()
    {
        var organization = new Organization
        {
            Name = $"Sessions migration {ObjectId.GenerateNewId()}",
            PlanId = GetService<BillingPlans>().FreePlan.Id,
            Data = new Exceptionless.Core.Models.DataDictionary
            {
                [PredefinedSavedViewsDataSeed.PredefinedSavedViewsContentHashDataKey] = "legacy-definitions"
            }
        };
        organization = await _organizationRepository.AddAsync(organization, options => options.ImmediateConsistency());

        var organizationSessionViews = await _savedViewRepository.GetByViewAsync(
            organization.Id,
            "sessions",
            options => options.PageLimit(1000).ImmediateConsistency());
        if (organizationSessionViews.Documents.Count > 0)
            await _savedViewRepository.RemoveAsync(organizationSessionViews.Documents, options => options.ImmediateConsistency());

        await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = organization.Id,
            UserId = PredefinedSavedViewsDataSeed.SystemUserId,
            CreatedByUserId = PredefinedSavedViewsDataSeed.SystemUserId,
            Name = "Private All Sessions",
            Slug = "all",
            ViewType = "sessions",
            Version = 1
        }, options => options.ImmediateConsistency());

        var systemViews = await _savedViewRepository.GetByOrganizationIdAsync(
            PredefinedSavedViewsDataSeed.SystemOrganizationId,
            options => options.PageLimit(1000).ImmediateConsistency());
        var sessionsViews = systemViews.Documents
            .Where(view => String.Equals(view.PredefinedKey, AddSessionsPredefinedSavedView.PredefinedKey, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (sessionsViews.Count > 0)
            await _savedViewRepository.RemoveAsync(sessionsViews, options => options.ImmediateConsistency());

        if (systemViews.Documents.Count == sessionsViews.Count)
        {
            var definitions = await PredefinedSavedViewsDataSeed.ReadDefaultSavedViewsAsync(TestCancellationToken);
            var legacyDefinition = definitions.Single(view => String.Equals(view.Key, "events:all", StringComparison.OrdinalIgnoreCase));
            await _savedViewRepository.AddAsync(
                PredefinedSavedViewsDataSeed.CreateSavedView(legacyDefinition),
                options => options.ImmediateConsistency());
        }

        var migration = GetService<AddSessionsPredefinedSavedView>();
        var context = new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken);
        await migration.RunAsync(context);
        await migration.RunAsync(context);

        var migratedViews = await _savedViewRepository.GetByOrganizationIdAsync(
            PredefinedSavedViewsDataSeed.SystemOrganizationId,
            options => options.PageLimit(1000).ImmediateConsistency());
        var sessionsView = Assert.Single(migratedViews.Documents, view =>
            String.Equals(view.PredefinedKey, AddSessionsPredefinedSavedView.PredefinedKey, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("sessions", sessionsView.ViewType);
        Assert.Equal("All", sessionsView.Name);
        Assert.Equal(PredefinedSavedViewContentHasher.GetContentHash(sessionsView), sessionsView.PredefinedContentHash);

        var migratedOrganizationViews = await _savedViewRepository.GetByViewAsync(
            organization.Id,
            "sessions",
            options => options.PageLimit(1000).ImmediateConsistency());
        var organizationSessionsView = Assert.Single(migratedOrganizationViews.Documents, view =>
            view.UserId is null && String.Equals(view.PredefinedKey, AddSessionsPredefinedSavedView.PredefinedKey, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("all-2", organizationSessionsView.Slug);
        Assert.Equal(PredefinedSavedViewContentHasher.GetContentHash(organizationSessionsView), organizationSessionsView.PredefinedContentHash);
    }

    [Fact]
    public async Task RunAsync_EmptySystemDefinitions_LeavesFreshInstallForDataSeed()
    {
        var systemViews = await _savedViewRepository.GetByOrganizationIdAsync(
            PredefinedSavedViewsDataSeed.SystemOrganizationId,
            options => options.PageLimit(1000).ImmediateConsistency());
        await _savedViewRepository.RemoveAsync(systemViews.Documents, options => options.ImmediateConsistency());

        var migration = GetService<AddSessionsPredefinedSavedView>();
        await migration.RunAsync(new MigrationContext(GetService<ILock>(), _logger, TestCancellationToken));

        Assert.Equal(0, await _savedViewRepository.CountByOrganizationIdAsync(PredefinedSavedViewsDataSeed.SystemOrganizationId));
    }
}
