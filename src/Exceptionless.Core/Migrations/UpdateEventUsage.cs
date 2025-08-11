using System.Diagnostics;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.DateTimeExtensions;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;
using Nest;

namespace Exceptionless.Core.Migrations;

public sealed class UpdateEventUsage : MigrationBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IEventRepository _eventRepository;
    private readonly ExceptionlessElasticConfiguration _config;
    private readonly TimeProvider _timeProvider;

    public UpdateEventUsage(
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository,
        IEventRepository eventRepository,
        ExceptionlessElasticConfiguration configuration,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _eventRepository = eventRepository;
        _config = configuration;
        _timeProvider = timeProvider;

        MigrationType = MigrationType.Repeatable;
    }

    public override async Task RunAsync(MigrationContext context)
    {
        _logger.LogInformation("Begin refreshing all indices");
        await _config.Client.Indices.RefreshAsync(Indices.All);
        _logger.LogInformation("Done refreshing all indices");

        await UpdateOrganizationsUsageAsync(context);
    }

    private async Task UpdateOrganizationsUsageAsync(MigrationContext context)
    {
        var organizationResults = await _organizationRepository.GetAllAsync(o => o.SoftDeleteMode(SoftDeleteQueryMode.All).SearchAfterPaging().PageLimit(5));
        _logger.LogInformation("Updating usage for {OrganizationTotal} organization(s)", organizationResults.Total);

        var sw = Stopwatch.StartNew();
        long total = organizationResults.Total;
        int processed = 0;
        int error = 0;
        var lastStatus = _timeProvider.GetUtcNow().UtcDateTime;

        while (organizationResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            foreach (var organization in organizationResults.Documents)
            {
                if (organization.MaxEventsPerMonth <= 0)
                {
                    processed++;
                    continue;
                }

                using var _ = _logger.BeginScope(new ExceptionlessState().Organization(organization.Id));
                try
                {
                    var result = await _eventRepository.CountAsync(q => q.Organization(organization.Id).AggregationsExpression("date:date~1M"));
                    var dateAggs = result.Aggregations.DateHistogram("date_date");
                    foreach (var dateHistogramBucket in dateAggs.Buckets)
                    {
                        var usage = organization.GetUsage(dateHistogramBucket.Date, _timeProvider);
                        long eventTotal = dateHistogramBucket.Total.GetValueOrDefault();
                        if (eventTotal > usage.Total)
                        {
                            _logger.LogInformation("Updating {OrganizationName} {UsageDate} usage total from {UsageTotalFrom} to {UsageTotal}", organization.Name, usage.Date, usage.Total, eventTotal);
                            usage.Total = (int)eventTotal;
                        }
                    }

                    await _organizationRepository.SaveAsync(organization);
                    await UpdateProjectsUsageAsync(context, organization);
                    processed++;
                    await context.Lock.RenewAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating organization {OrganizationId}: {Message}", organization.Id, ex.Message);
                    error++;
                }
            }

            if (_timeProvider.GetUtcNow().UtcDateTime.Subtract(lastStatus) > TimeSpan.FromSeconds(5))
            {
                lastStatus = _timeProvider.GetUtcNow().UtcDateTime;
                _logger.LogInformation("Total={Processed}/{Total} Errors={ErrorCount} Duration={Duration}", processed, total, error, sw.Elapsed.ToWords());
            }

            // Sleep so we are not hammering the backend.
            await Task.Delay(TimeSpan.FromSeconds(2.5), _timeProvider);
            if (context.CancellationToken.IsCancellationRequested || !await organizationResults.NextPageAsync())
                break;
        }
    }

    private async Task UpdateProjectsUsageAsync(MigrationContext context, Organization organization)
    {
        var projectResults = await _projectRepository.GetByOrganizationIdAsync(organization.Id, o => o.SoftDeleteMode(SoftDeleteQueryMode.All).SearchAfterPaging().PageLimit(100));
        _logger.LogInformation("Updating usage for {ProjectTotal} projects(s)", projectResults.Total);

        var sw = Stopwatch.StartNew();
        while (projectResults.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            foreach (var project in projectResults.Documents)
            {
                using var _ = _logger.BeginScope(new ExceptionlessState().Organization(organization.Id).Project(project.Id));
                try
                {
                    var result = await _eventRepository.CountAsync(q => q.Organization(organization.Id).Project(project.Id).AggregationsExpression("date:date~1M"));
                    var dateAggs = result.Aggregations.DateHistogram("date_date");
                    foreach (var dateHistogramBucket in dateAggs.Buckets)
                    {
                        var usage = project.GetUsage(dateHistogramBucket.Date);
                        long eventTotal = dateHistogramBucket.Total.GetValueOrDefault();
                        if (eventTotal > usage.Total)
                        {
                            _logger.LogInformation("Updating {ProjectName} ({ProjectId}) {UsageDate} usage total from {UsageTotalFrom} to {UsageTotal}", project.Name, project.Id, usage.Date, usage.Total, eventTotal);
                            usage.Total = (int)eventTotal;
                        }

                        if (usage.Limit == 0)
                            usage.Limit = organization.GetMaxEventsPerMonthWithBonus(_timeProvider);
                    }

                    await _projectRepository.SaveAsync(project);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating project {ProjectId}: {Message}", project.Id, ex.Message);
                }
            }

            if (context.CancellationToken.IsCancellationRequested || !await projectResults.NextPageAsync())
                break;
        }
    }
}
