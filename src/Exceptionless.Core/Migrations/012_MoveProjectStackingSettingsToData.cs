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

        // Patch in place instead of saving whole documents, so a project edited while the migration runs isn't
        // overwritten with the snapshot read above. Projects without these settings are left untouched (noop).
        var patch = new ScriptPatch("""
            def config = ctx._source.configuration;
            def settings = config == null ? null : config.settings;
            boolean moved = false;
            if (settings != null) {
                for (def key : params.keys) {
                    if (!settings.containsKey(key)) {
                        continue;
                    }

                    def value = settings.remove(key);
                    moved = true;
                    if (value == null || value.toString().trim().isEmpty()) {
                        continue;
                    }

                    if (ctx._source.data == null) {
                        ctx._source.data = new HashMap();
                    }

                    ctx._source.data[key] = value.toString().trim();
                }
            }

            if (moved) {
                config.version = (config.version == null ? 0 : config.version) + 1;
            } else {
                ctx.op = 'noop';
            }
            """)
        {
            Params = new Dictionary<string, object> { ["keys"] = SettingKeys }
        };

        long scanned = 0;
        long patched = 0;
        while (projects.Documents.Count is > 0)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var ids = projects.Documents
                .Where(project => SettingKeys.Any(key => project.Configuration?.Settings.ContainsKey(key) is true))
                .Select(project => project.Id)
                .ToArray();

            if (ids.Length > 0)
                patched += await _projectRepository.PatchAsync(ids, patch, options => options.Cache().Notifications(false));

            scanned += projects.Documents.Count;
            _logger.LogInformation("Scanned {ProjectCount} projects and moved stacking settings for {PatchedCount} so far", scanned, patched);
            await context.Lock.RenewAsync();

            if (!await projects.NextPageAsync())
                break;
        }

        _logger.LogInformation("Moved stacking settings to project data for {PatchedCount} of {ProjectCount} projects", patched, scanned);
    }
}
