using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models.Admin;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Storage;
using Foundatio.Mediator;

namespace Exceptionless.Web.Api.Handlers;

public class AdminHandler(
    ExceptionlessElasticConfiguration configuration,
    IFileStorage fileStorage,
    IMessagePublisher messagePublisher,
    IOrganizationRepository organizationRepository,
    IProjectRepository projectRepository,
    IStackRepository stackRepository,
    IEventRepository eventRepository,
    IUserRepository userRepository,
    IQueue<EventPost> eventPostQueue,
    IQueue<WorkItemData> workItemQueue,
    AppOptions appOptions,
    BillingManager billingManager,
    BillingPlans plans,
    IMigrationStateRepository migrationStateRepository,
    SampleDataService sampleDataService,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<AdminHandler>();

    [HandlerEndpoint(HandlerMethod.Get, "settings", Group = "Admin")]
    public Task<Result<object>> Handle(GetAdminSettings message)
    {
        return Task.FromResult<Result<object>>(appOptions);
    }

    [HandlerEndpoint(HandlerMethod.Get, "stats", Group = "Admin")]
    public async Task<Result<object>> Handle(GetAdminStats message)
    {
        var organizationCountTask = organizationRepository.CountAsync(q => q
            .AggregationsExpression("terms:billing_status date:created_utc~1M"));

        var userCountTask = userRepository.CountAsync();
        var projectCountTask = projectRepository.CountAsync();

        var stackCountTask = stackRepository.CountAsync(q => q
            .AggregationsExpression("terms:status terms:(type terms:status)"));

        var eventCountTask = eventRepository.CountAsync(q => q
            .AggregationsExpression("date:date~1M"));

        await Task.WhenAll(organizationCountTask, userCountTask, projectCountTask, stackCountTask, eventCountTask);

        return new AdminStatsResponse(
            Organizations: await organizationCountTask,
            Users: await userCountTask,
            Projects: await projectCountTask,
            Stacks: await stackCountTask,
            Events: await eventCountTask
        );
    }

    [HandlerEndpoint(HandlerMethod.Get, "migrations", Group = "Admin")]
    public async Task<Result<object>> Handle(GetAdminMigrations message)
    {
        var result = await migrationStateRepository.GetAllAsync(o => o.SearchAfterPaging().PageLimit(1000));
        var migrationStates = new List<MigrationState>(result.Documents.Count);

        while (result.Documents.Count > 0)
        {
            migrationStates.AddRange(result.Documents);

            if (!await result.NextPageAsync())
                break;
        }

        var states = migrationStates
            .OrderByDescending(s => s.Version)
            .ThenByDescending(s => s.StartedUtc)
            .ToArray();

        int currentVersion = states
            .Where(s => s.MigrationType != MigrationType.Repeatable && s.CompletedUtc.HasValue)
            .Select(s => s.Version)
            .DefaultIfEmpty(-1)
            .Max();

        return new MigrationsResponse(currentVersion, states);
    }

    public Task<Result<object>> Handle(GetAdminEcho message)
    {
        var httpContext = message.Context;
        return Task.FromResult<Result<object>>(new
        {
            httpContext.Request.Headers,
            IpAddress = httpContext.Request.GetClientIpAddress()
        });
    }

    [HandlerEndpoint(HandlerMethod.Get, "assemblies", Group = "Admin")]
    public Task<Result<object>> Handle(GetAdminAssemblies message)
    {
        var details = AssemblyDetail.ExtractAll().Select(AssemblyDetailResponse.FromAssemblyDetail).ToArray();
        return Task.FromResult(Result<object>.Success(details));
    }

    public async Task<Result<object>> Handle(AdminChangePlan message)
    {
        var httpContext = message.Context;
        if (String.IsNullOrEmpty(message.OrganizationId) || !httpContext.Request.CanAccessOrganization(message.OrganizationId))
            return new ChangePlanResponse(false, "Invalid Organization Id.");

        var organization = await organizationRepository.GetByIdAsync(message.OrganizationId);
        if (organization is null)
            return new ChangePlanResponse(false, "Invalid Organization Id.");

        var plan = billingManager.GetBillingPlan(message.PlanId);
        if (plan is null)
            return new ChangePlanResponse(false, "Invalid PlanId.");

        organization.BillingStatus = !String.Equals(plan.Id, plans.FreePlan.Id) ? BillingStatus.Active : BillingStatus.Trialing;
        organization.RemoveSuspension();
        var currentUser = httpContext.Request.GetUser();
        billingManager.ApplyBillingPlan(organization, plan, currentUser, false);

        await organizationRepository.SaveAsync(organization, o => o.Cache().Originals());
        await messagePublisher.PublishAsync(new PlanChanged
        {
            OrganizationId = organization.Id
        });

        return new ChangePlanResponse(true);
    }

    public async Task<Result> Handle(AdminSetBonus message)
    {
        var httpContext = message.Context;
        if (String.IsNullOrEmpty(message.OrganizationId) || !httpContext.Request.CanAccessOrganization(message.OrganizationId))
            return Result.Invalid(ValidationError.Create("organizationId", "Invalid Organization Id"));

        var organization = await organizationRepository.GetByIdAsync(message.OrganizationId);
        if (organization is null)
            return Result.Invalid(ValidationError.Create("organizationId", "Invalid Organization Id"));

        billingManager.ApplyBonus(organization, message.BonusEvents, message.Expires);
        await organizationRepository.SaveAsync(organization, o => o.Cache().Originals());

        return Result.Success();
    }

    [HandlerEndpoint(HandlerMethod.Get, "requeue", Group = "Admin")]
    public async Task<Result<object>> Handle(AdminRequeue message)
    {
        string path = message.Path ?? @"q\*";

        int enqueued = 0;
        foreach (var file in await fileStorage.GetFileListAsync(path))
        {
            await eventPostQueue.EnqueueAsync(new EventPost(appOptions.EnableArchive && message.Archive) { FilePath = file.Path });
            enqueued++;
        }

        return new { Enqueued = enqueued };
    }

    [HandlerEndpoint(HandlerMethod.Get, "maintenance/{name:minlength(1)}", Group = "Admin")]
    public async Task<Result> Handle(AdminRunMaintenance message)
    {
        switch (message.Name.ToLowerInvariant())
        {
            case "fix-stack-stats":
                var effectiveUtcStart = message.UtcStart ?? timeProvider.GetUtcNow().UtcDateTime.AddDays(-90);

                if (message.UtcEnd.HasValue && message.UtcEnd.Value.IsBefore(effectiveUtcStart))
                    return Result.Invalid(ValidationError.Create("utc_end", "utcEnd must be greater than or equal to utcStart."));

                await workItemQueue.EnqueueAsync(new FixStackStatsWorkItem
                {
                    UtcStart = effectiveUtcStart,
                    UtcEnd = message.UtcEnd,
                    OrganizationId = message.OrganizationId
                });
                break;
            case "increment-project-configuration-version":
                await workItemQueue.EnqueueAsync(new ProjectMaintenanceWorkItem { IncrementConfigurationVersion = true });
                break;
            case "indexes":
                if (!appOptions.ElasticsearchOptions.DisableIndexConfiguration)
                    await configuration.ConfigureIndexesAsync(beginReindexingOutdated: false);
                break;
            case "normalize-user-email-address":
                await workItemQueue.EnqueueAsync(new UserMaintenanceWorkItem { Normalize = true });
                break;
            case "remove-old-organization-usage":
                await workItemQueue.EnqueueAsync(new OrganizationMaintenanceWorkItem { RemoveOldUsageStats = true });
                break;
            case "remove-old-project-usage":
                await workItemQueue.EnqueueAsync(new ProjectMaintenanceWorkItem { RemoveOldUsageStats = true });
                break;
            case "reset-verify-email-address-token-and-expiration":
                await workItemQueue.EnqueueAsync(new UserMaintenanceWorkItem { ResetVerifyEmailAddressToken = true });
                break;
            case "update-organization-plans":
                await workItemQueue.EnqueueAsync(new OrganizationMaintenanceWorkItem { UpgradePlans = true });
                break;
            case "update-project-default-bot-lists":
                await workItemQueue.EnqueueAsync(new ProjectMaintenanceWorkItem { UpdateDefaultBotList = true, IncrementConfigurationVersion = true });
                break;
            case "update-project-notification-settings":
                await workItemQueue.EnqueueAsync(new UpdateProjectNotificationSettingsWorkItem
                {
                    OrganizationId = message.OrganizationId
                });
                break;
            default:
                return Result.NotFound("Maintenance action not found.");
        }

        return Result.Success();
    }

    [HandlerEndpoint(HandlerMethod.Get, "elasticsearch", Group = "Admin")]
    public async Task<Result<object>> Handle(GetAdminElasticsearch message)
    {
        var client = configuration.Client;
        var healthTask = client.Cluster.HealthAsync(r => r.Level(Elastic.Clients.Elasticsearch.Level.Indices));
        var statsTask = client.Cluster.StatsAsync();
        var indicesStatsTask = client.Indices.StatsAsync();
        await Task.WhenAll(healthTask, statsTask, indicesStatsTask);

        var healthResponse = await healthTask;
        var statsResponse = await statsTask;
        var indicesStatsResponse = await indicesStatsTask;

        if (!healthResponse.IsValidResponse || !statsResponse.IsValidResponse || !indicesStatsResponse.IsValidResponse)
            return Result.Error("Elasticsearch cluster information is unavailable.");

        var unassignedByIndex = (healthResponse.Indices ?? new Dictionary<string, Elastic.Clients.Elasticsearch.Cluster.IndexHealthStats>())
            .Where(kvp => kvp.Value.UnassignedShards > 0)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.UnassignedShards, StringComparer.OrdinalIgnoreCase);

        var indexDetails = (indicesStatsResponse.Indices ?? new Dictionary<string, Elastic.Clients.Elasticsearch.IndexManagement.IndicesStats>())
            .OrderByDescending(kvp => kvp.Value.Total?.Store?.SizeInBytes ?? 0)
            .Select(kvp => new ElasticsearchIndexDetailResponse(
                Index: kvp.Key,
                Health: kvp.Value.Health?.ToString().ToLowerInvariant(),
                Status: kvp.Value.Status?.ToString().ToLowerInvariant(),
                Primary: healthResponse.Indices?.GetValueOrDefault(kvp.Key)?.NumberOfShards ?? 0,
                Replica: healthResponse.Indices?.GetValueOrDefault(kvp.Key)?.NumberOfReplicas ?? 0,
                DocsCount: kvp.Value.Total?.Docs?.Count ?? 0,
                StoreSizeInBytes: kvp.Value.Total?.Store?.SizeInBytes ?? 0,
                UnassignedShards: unassignedByIndex.GetValueOrDefault(kvp.Key, 0)
            ))
            .ToArray();

        return new ElasticsearchInfoResponse(
            Health: new ElasticsearchHealthResponse(
                Status: (int)healthResponse.Status,
                ClusterName: healthResponse.ClusterName,
                NumberOfNodes: healthResponse.NumberOfNodes,
                NumberOfDataNodes: healthResponse.NumberOfDataNodes,
                ActiveShards: healthResponse.ActiveShards,
                RelocatingShards: healthResponse.RelocatingShards,
                UnassignedShards: healthResponse.UnassignedShards,
                ActivePrimaryShards: healthResponse.ActivePrimaryShards
            ),
            Indices: new ElasticsearchIndicesResponse(
                Count: statsResponse.Indices.Count,
                DocsCount: statsResponse.Indices.Docs.Count,
                StoreSizeInBytes: statsResponse.Indices.Store.SizeInBytes
            ),
            IndexDetails: indexDetails
        );
    }

    [HandlerEndpoint(HandlerMethod.Get, "elasticsearch/snapshots", Group = "Admin")]
    public async Task<Result<object>> Handle(GetAdminElasticsearchSnapshots message)
    {
        var client = configuration.Client;
        try
        {
            var repositoryResponse = await client.Snapshot.GetRepositoryAsync();
            if (!repositoryResponse.IsValidResponse)
                return Result.Error("Snapshot repository information is unavailable.");

            if (repositoryResponse.Repositories is null || !repositoryResponse.Repositories.Any())
                return new ElasticsearchSnapshotsResponse([], []);

            var repositoryNames = repositoryResponse.Repositories.Select(r => r.Key).ToArray();

            var snapshotTasks = repositoryNames
                .Select(async repositoryName =>
                {
                    var snapshotResponse = await client.Snapshot.GetAsync(repositoryName, "*");
                    if (!snapshotResponse.IsValidResponse)
                        return (
                            RepositoryName: repositoryName,
                            Snapshots: Array.Empty<ElasticsearchSnapshotResponse>(),
                            Error: $"Unable to retrieve snapshots for repository: {repositoryName}."
                        );

                    var snapshots = snapshotResponse.Snapshots?.ToArray() ?? [];
                    return (
                        RepositoryName: repositoryName,
                        Snapshots: snapshots.Select(s => new ElasticsearchSnapshotResponse(
                            Repository: repositoryName,
                            Name: s.Snapshot,
                            Status: s.State ?? String.Empty,
                            StartTime: s.StartTime?.UtcDateTime,
                            EndTime: s.EndTime?.UtcDateTime,
                            Duration: s.Duration?.ToString() ?? String.Empty,
                            IndicesCount: s.Indices?.Count ?? 0,
                            SuccessfulShards: s.Shards?.Successful ?? 0,
                            FailedShards: s.Shards?.Failed ?? 0,
                            TotalShards: s.Shards?.Total ?? 0
                        )).ToArray(),
                        Error: (string?)null
                    );
                })
                .ToArray();

            var snapshotResults = await Task.WhenAll(snapshotTasks);

            var failedSnapshotResults = snapshotResults
                .Where(r => r.Error is not null)
                .ToArray();

            if (failedSnapshotResults.Length is > 0)
            {
                _logger.LogWarning("Unable to retrieve snapshots for one or more repositories: {Repositories}",
                    String.Join(", ", failedSnapshotResults.Select(r => r.RepositoryName)));
            }

            var successfulSnapshotResults = snapshotResults
                .Where(r => r.Error is null)
                .ToArray();

            if (successfulSnapshotResults.Length is 0)
                return Result.Error("Unable to retrieve snapshot information.");

            var snapshots = successfulSnapshotResults
                .SelectMany(r => r.Snapshots)
                .OrderByDescending(s => s.StartTime)
                .ToArray();

            var successfulRepositoryNames = successfulSnapshotResults
                .Select(r => r.RepositoryName)
                .ToArray();

            return new ElasticsearchSnapshotsResponse(successfulRepositoryNames, snapshots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to retrieve snapshot information");
            return Result.Error("Unable to retrieve snapshot information.");
        }
    }

    [HandlerEndpoint(HandlerMethod.Post, "generate-sample-events", Group = "Admin")]
    public async Task<Result<object>> Handle(AdminGenerateSampleEvents message)
    {
        if (message.EventCount < 1 || message.EventCount > 10000)
            return Result.Invalid(ValidationError.Create("eventCount", "Event count must be between 1 and 10,000."));

        if (message.DaysBack < 1 || message.DaysBack > 365)
            return Result.Invalid(ValidationError.Create("daysBack", "Days back must be between 1 and 365."));

        await sampleDataService.EnqueueSampleEventsAsync(message.EventCount, message.DaysBack);
        return new { Success = true, Message = $"Enqueued generation of {message.EventCount} sample events over {message.DaysBack} days. Events will appear shortly." };
    }
}
