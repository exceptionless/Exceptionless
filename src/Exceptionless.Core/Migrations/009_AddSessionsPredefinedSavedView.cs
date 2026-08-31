using Exceptionless.Core.Repositories;
using Exceptionless.Core.Seed;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

public sealed class AddSessionsPredefinedSavedView : MigrationBase
{
    internal const string PredefinedKey = "sessions:all";
    private readonly ISavedViewRepository _savedViewRepository;

    public AddSessionsPredefinedSavedView(
        ISavedViewRepository savedViewRepository,
        ILoggerFactory loggerFactory) : base(loggerFactory)
    {
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
        if (existingResults.Total == 0 || existingResults.Documents.Any(view =>
            String.Equals(view.PredefinedKey, PredefinedKey, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var definitions = await PredefinedSavedViewsDataSeed.ReadDefaultSavedViewsAsync(context.CancellationToken);
        var definition = definitions.Single(view => String.Equals(view.Key, PredefinedKey, StringComparison.OrdinalIgnoreCase));
        var savedView = PredefinedSavedViewsDataSeed.CreateSavedView(definition);

        await _savedViewRepository.AddAsync(savedView, options => options.Cache().ImmediateConsistency());
        _logger.LogInformation("Added the Sessions predefined saved view to the existing system definitions");
    }
}
