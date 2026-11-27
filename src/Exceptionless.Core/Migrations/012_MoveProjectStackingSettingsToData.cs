using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Migrations;

/// <summary>
/// The event pipeline reads the User Namespaces and Common Methods stacking settings from project data, but the new
/// app saved them into the project's client configuration where they never affected stacking. This moves them to
/// project data. The configuration value wins over existing project data since it came from the newer UI.
/// </summary>
public sealed class MoveProjectStackingSettingsToData : MigrationBase
{
    private const int BatchSize = 100;
    private static readonly string[] SettingKeys = ["UserNamespaces", "CommonMethods"];
    private readonly IProjectRepository _projectRepository;

    public MoveProjectStackingSettingsToData(IProjectRepository projectRepository, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _projectRepository = projectRepository;

        MigrationType = MigrationType.VersionedAndResumable;
        Version = 12;
    }

    public override async Task RunAsync(MigrationContext context)
    {
        var projects = await _projectRepository.FindAsync(
            query => query.SortAscending(project => project.Id),
            options => options
                .SoftDeleteMode(SoftDeleteQueryMode.All)
                .SearchAfterPaging()
                .PageLimit(BatchSize));

        long scanned = 0;
        long migrated = 0;
        long values = 0;
        while (projects.Documents.Count is > 0)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var changed = new List<Project>();
            foreach (var project in projects.Documents)
            {
                int moved = MoveSettings(project);
                if (moved is 0)
                    continue;

                changed.Add(project);
                values += moved;
            }

            if (changed.Count > 0)
            {
                await _projectRepository.SaveAsync(changed, options => options.Cache().Notifications(false));
                migrated += changed.Count;
            }

            scanned += projects.Documents.Count;
            _logger.LogInformation("Scanned {ProjectCount} projects and moved stacking settings for {MigratedCount} so far", scanned, migrated);
            await context.Lock.RenewAsync();

            if (!await projects.NextPageAsync())
                break;
        }

        _logger.LogInformation("Moved {ValueCount} stacking settings to project data for {MigratedCount} of {ProjectCount} projects", values, migrated, scanned);
    }

    private static int MoveSettings(Project project)
    {
        var settings = project.Configuration?.Settings;
        if (settings is null)
            return 0;

        int moved = 0;
        foreach (string key in SettingKeys)
        {
            if (!settings.TryGetValue(key, out string? value))
                continue;

            settings.Remove(key);
            moved++;

            if (String.IsNullOrWhiteSpace(value))
                continue;

            project.Data ??= new DataDictionary();
            project.Data[key] = value.Trim();
        }

        if (moved > 0)
            project.Configuration!.IncrementVersion();

        return moved;
    }
}
