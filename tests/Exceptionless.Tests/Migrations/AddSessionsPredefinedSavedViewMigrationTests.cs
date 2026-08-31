using Exceptionless.Core.Migrations;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Seed;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Utility;
using Xunit;

namespace Exceptionless.Tests.Migrations;

public sealed class AddSessionsPredefinedSavedViewMigrationTests : IntegrationTestsBase
{
    private readonly ISavedViewRepository _savedViewRepository;

    public AddSessionsPredefinedSavedViewMigrationTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
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
