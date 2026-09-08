using Exceptionless.Core.Jobs.WorkItemHandlers;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Seed;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

public sealed class AddSessionsPredefinedSavedView : MigrationBase
{
    private const int PageLimit = 500;
    internal const string PredefinedKey = "sessions:all";
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISavedViewRepository _savedViewRepository;

    public AddSessionsPredefinedSavedView(
        IOrganizationRepository organizationRepository,
        ISavedViewRepository savedViewRepository,
        ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _savedViewRepository = savedViewRepository;

        MigrationType = MigrationType.VersionedAndResumable;
        Version = 9;
    }

    public override async Task RunAsync(MigrationContext context)
    {
        var existingResults = await _savedViewRepository.GetByOrganizationIdAsync(
            PredefinedSavedViewsDataSeed.SystemOrganizationId,
            options => options.PageLimit(1000).ImmediateConsistency());

        // A fresh installation has no system definitions yet; the data seed creates the complete
        // set after migrations finish. This migration only upgrades an existing nonempty seed.
        if (existingResults.Total == 0)
            return;

        var definitions = await PredefinedSavedViewsDataSeed.ReadDefaultSavedViewsAsync(context.CancellationToken);
        var definition = definitions.Single(view => String.Equals(view.Key, PredefinedKey, StringComparison.OrdinalIgnoreCase));
        if (!existingResults.Documents.Any(view => String.Equals(view.PredefinedKey, PredefinedKey, StringComparison.OrdinalIgnoreCase)))
        {
            var savedView = PredefinedSavedViewsDataSeed.CreateSavedView(definition);
            await _savedViewRepository.AddAsync(savedView, options => options.Cache().ImmediateConsistency());
        }

        int organizationsUpdated = 0;
        var organizations = await _organizationRepository.FindAsync(
            query => query.SortAscending(organization => organization.Id),
            options => options.SearchAfterPaging().PageLimit(PageLimit));
        do
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            foreach (var organization in organizations.Documents.Where(PredefinedSavedViewsDataSeed.HasCreatedPredefinedSavedViews))
            {
                var existingSessionsViews = await _savedViewRepository.GetByViewAsync(
                    organization.Id,
                    "sessions",
                    options => options.PageLimit(1000));
                if (existingSessionsViews.Documents.Any(view =>
                    view.UserId is null && String.Equals(view.PredefinedKey, PredefinedKey, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                string slug = ForcePredefinedSavedViewsWorkItemHandler.GetUniqueSlug(definition.Slug, existingSessionsViews.Documents, null);
                var savedView = PredefinedSavedViewsDataSeed.CreateSavedView(
                    definition,
                    organization.Id,
                    PredefinedSavedViewsDataSeed.SystemUserId,
                    slug);
                await _savedViewRepository.AddAsync(savedView, options => options.Cache().ImmediateConsistency());
                organizationsUpdated++;
            }

            await context.Lock.RenewAsync();
        } while (!context.CancellationToken.IsCancellationRequested && await organizations.NextPageAsync());

        context.CancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation(
            "Added the Sessions predefined saved view to the system definitions and {OrganizationCount} existing organizations",
            organizationsUpdated);
    }
}
