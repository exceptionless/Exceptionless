using System.Diagnostics;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Services;
using Foundatio.Caching;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Nest;

namespace Exceptionless.Core.Migrations;

public sealed class UpdateEventUsage : MigrationBase {
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IEventRepository _eventRepository;
    private readonly UsageService _usageService;
    private readonly ICacheClient _cache;
    private readonly ExceptionlessElasticConfiguration _config;

    public UpdateEventUsage(
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository,
        IEventRepository eventRepository,
        UsageService usageService,
        ICacheClient cache,
        ExceptionlessElasticConfiguration configuration, 
        ILoggerFactory loggerFactory) : base(loggerFactory) 
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _eventRepository = eventRepository;
        _usageService = usageService;
        _cache = cache;
        _config = configuration;

        MigrationType = MigrationType.VersionedAndResumable;
        Version = 3;
    }

    public override async Task RunAsync(MigrationContext context) {
        _logger.LogInformation("Begin refreshing all indices");
        await _config.Client.Indices.RefreshAsync(Indices.All);
        _logger.LogInformation("Done refreshing all indices");
        
        await UpdateOrganizationsUsageAsync(context);
    }


    private async Task UpdateOrganizationsUsageAsync(MigrationContext context) {
        var organizationResults = await _organizationRepository.GetAllAsync(o => o.SoftDeleteMode(SoftDeleteQueryMode.All).SearchAfterPaging().PageLimit(5)).AnyContext();
        _logger.LogInformation("Updating usage for {OrganizationTotal} organization(s)", organizationResults.Total);

        var sw = Stopwatch.StartNew();
        while (organizationResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
            foreach (var organization in organizationResults.Documents) {
                using var _ = _logger.BeginScope(new ExceptionlessState().Organization(organization.Id));
                try {
                    var result = await _eventRepository.CountAsync(q => q.Organization(organization.Id).AggregationsExpression("date:createdUtc~1M"));
                    var dateAggs = result.Aggregations.DateHistogram("date_createdUtc");
                    foreach (var dateHistogramBucket in dateAggs.Buckets) {
                        var usage = organization.GetUsage(dateHistogramBucket.Date);
                        var total = dateHistogramBucket.Total.GetValueOrDefault();
                        if (total > usage.Total) {
                            _logger.LogInformation("Updating {OrganizationName} {UsageDate} usage total from {UsageTotalFrom} to {UsageTotal}", organization.Name, usage.Total, total);
                            usage.Total = (int)total;
                        }
                    }

                    await _organizationRepository.SaveAsync(organization);
                    await UpdateProjectsUsageAsync(context, organization);
                    await context.Lock.RenewAsync();

                } catch (Exception ex) {
                    _logger.LogError(ex, "Error updating organization {OrganizationId}: {Message}", organization.Id, ex.Message);
                }
            }

            _logger.LogInformation("Script operation task ({TaskId}) completed: Created: {Created} Updated: {Updated} Deleted: {Deleted} Conflicts: {Conflicts} Total: {Total}", taskId, status.Created, status.Updated, status.Deleted, status.VersionConflicts, status.Total);
            // Sleep so we are not hammering the backend.
            await SystemClock.SleepAsync(TimeSpan.FromSeconds(2.5)).AnyContext();
            if (context.CancellationToken.IsCancellationRequested || !await organizationResults.NextPageAsync().AnyContext())
                break;
        }
    }

    private async Task UpdateProjectsUsageAsync(MigrationContext context, Organization organization) {
        var projectResults = await _projectRepository.GetByOrganizationIdAsync(organization.Id, o => o.SoftDeleteMode(SoftDeleteQueryMode.All).SearchAfterPaging().PageLimit(100)).AnyContext();
        _logger.LogInformation("Updating usage for {ProjectTotal} projects(s)", projectResults.Total);

        var sw = Stopwatch.StartNew();
        while (projectResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested) {
            foreach (var project in projectResults.Documents) {
                using var _ = _logger.BeginScope(new ExceptionlessState().Organization(organization.Id).Project(project.Id));
                try {
                    var result = await _eventRepository.CountAsync(q => q.Organization(organization.Id).Project(project.Id).AggregationsExpression("date:createdUtc~1M"));
                    var dateAggs = result.Aggregations.DateHistogram("date_createdUtc");
                    foreach (var dateHistogramBucket in dateAggs.Buckets) {
                        var usage = project.GetUsage(dateHistogramBucket.Date);
                        var total = dateHistogramBucket.Total.GetValueOrDefault();
                        if (total > usage.Total) {
                            _logger.LogInformation("Updating {ProjectName} ({ProjectId}) {UsageDate} usage total from {UsageTotalFrom} to {UsageTotal}", project.Name, project.Id, usage.Total, total);
                            usage.Total = (int)total;
                        }
                    }

                    await _projectRepository.SaveAsync(project);

                } catch (Exception ex) {
                    _logger.LogError(ex, "Error updating project {ProjectId}: {Message}", project.Id, ex.Message);
                }
            }

            if (context.CancellationToken.IsCancellationRequested || !await projectResults.NextPageAsync().AnyContext())
                break;
        }
    }
}
