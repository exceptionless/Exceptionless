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
using Exceptionless.Web.Api.Filters;
using Exceptionless.Web.Api.Infrastructure;
using Exceptionless.Web.Api.Messages;
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models.Admin;
using Exceptionless.Core.Authorization;
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

    [HiddenEndpoint]
    [HandlerAuthorize(Policies = [AuthorizationRoles.GlobalAdminPolicy])]
    [HandlerEndpoint(HandlerMethod.Get, "/api/v2/admin/settings", EndpointFilters = [typeof(AutoValidationEndpointFilter)])]
    public Task<Result<object>> Handle(GetAdminSettings message)
    {
        return Task.FromResult<Result<object>>(appOptions);
    }

    [HiddenEndpoint]
    [HandlerAuthorize(Policies = [AuthorizationRoles.GlobalAdminPolicy])]
    [HandlerEndpoint(HandlerMethod.Get, "/api/v2/admin/stats", EndpointFilters = [typeof(AutoValidationEndpointFilter)])]
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

    [HiddenEndpoint]
    [HandlerAuthorize(Policies = [AuthorizationRoles.GlobalAdminPolicy])]
    [HandlerEndpoint(HandlerMethod.Get, "/api/v2/admin/migrations", EndpointFilters = [typeof(AutoValidationEndpointFilter)])]
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

    [HiddenEndpoint]
    [HandlerAuthorize(Policies = [AuthorizationRoles.GlobalAdminPolicy])]
    [HandlerEndpoint(HandlerMethod.Get, "/api/v2/admin/assemblies", EndpointFilters = [typeof(AutoValidationEndpointFilter)])]
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

    [HiddenEndpoint]
    [HandlerAuthorize(Policies = [AuthorizationRoles.GlobalAdminPolicy])]
    [HandlerEndpoint(HandlerMethod.Get, "/api/v2/admin/requeue", EndpointFilters = [typeof(AutoValidationEndpointFilter)])]
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

    [HiddenEndpoint]
    [HandlerAuthorize(Policies = [AuthorizationRoles.GlobalAdminPolicy])]
    [HandlerEndpoint(HandlerMethod.Get, "/api/v2/admin/maintenance/{name:minlength(1)}", EndpointFilters = [typeof(AutoValidationEndpointFilter)])]
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

    [HiddenEndpoint]
    [HandlerAuthorize(Policies = [AuthorizationRoles.GlobalAdminPolicy])]
    [HandlerEndpoint(HandlerMethod.Get, "/api/v2/admin/elasticsearch", EndpointFilters = [typeof(AutoValidationEndpointFilter)])]
    public async Task<Result<object>> Handle(GetAdminElasticsearch message)
    {
        var client = configuration.Client;
        var healthTask = client.Cluster.HealthAsync();
        var statsTask = client.Cluster.StatsAsync();
        var catIndicesTask = client.Cat.IndicesAsync(r => r.Bytes(Elasticsearch.Net.Bytes.B));
        var catShardsTask = client.Cat.ShardsAsync();
        await Task.WhenAll(healthTask, statsTask, catIndicesTask, catShardsTask);

        var healthResponse = await healthTask;
        var statsResponse = await statsTask;
        var catIndicesResponse = await catIndicesTask;
        var catShardsResponse = await catShardsTask;

        if (!healthResponse.IsValid || !statsResponse.IsValid || !catIndicesResponse.IsValid || !catShardsResponse.IsValid)
            return Result.Error("Elasticsearch cluster information is unavailable.");

        var unassignedByIndex = (catShardsResponse.Records ?? [])
            .Where(s => string.Equals(s.State, "UNASSIGNED", StringComparison.OrdinalIgnoreCase))
            .GroupBy(s => s.Index ?? String.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var indexDetails = (catIndicesResponse.Records ?? [])
            .OrderByDescending(i => long.TryParse(i.StoreSize, out var s) ? s : 0)
            .Select(i => new ElasticsearchIndexDetailResponse(
                Index: i.Index,
                Health: i.Health,
                Status: i.Status,
                Primary: int.TryParse(i.Primary, out var p) ? p : 0,
                Replica: int.TryParse(i.Replica, out var r) ? r : 0,
                DocsCount: long.TryParse(i.DocsCount, out var dc) ? dc : 0,
                StoreSizeInBytes: long.TryParse(i.StoreSize, out var ss) ? ss : 0,
                UnassignedShards: unassignedByIndex.GetValueOrDefault(i.Index ?? String.Empty, 0)
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
                DocsCount: statsResponse.Indices.Documents.Count,
                StoreSizeInBytes: statsResponse.Indices.Store.SizeInBytes
            ),
            IndexDetails: indexDetails
        );
    }

    [HiddenEndpoint]
    [HandlerAuthorize(Policies = [AuthorizationRoles.GlobalAdminPolicy])]
    [HandlerEndpoint(HandlerMethod.Get, "/api/v2/admin/elasticsearch/snapshots", EndpointFilters = [typeof(AutoValidationEndpointFilter)])]
    public async Task<Result<object>> Handle(GetAdminElasticsearchSnapshots message)
    {
        var client = configuration.Client;
        try
        {
            var repositoryResponse = await client.Cat.RepositoriesAsync();
            if (!repositoryResponse.IsValid)
                return Result.Error("Snapshot repository information is unavailable.");

            if (!(repositoryResponse.Records?.Any() ?? false))
                return new ElasticsearchSnapshotsResponse([], []);

            var repositoryNames = repositoryResponse.Records
                .Where(r => !String.IsNullOrEmpty(r.Id))
                .Select(r => r.Id!)
                .ToArray();

            var snapshotTasks = repositoryNames
                .Select(async repositoryName =>
                {
                    var snapshotResponse = await client.Cat.SnapshotsAsync(r => r.RepositoryName(repositoryName));
                    if (!snapshotResponse.IsValid)
                        return (
                            RepositoryName: repositoryName,
                            Snapshots: Array.Empty<ElasticsearchSnapshotResponse>(),
                            Error: $"Unable to retrieve snapshots for repository: {repositoryName}."
                        );

                    var snapshotRecords = snapshotResponse.Records?.ToArray() ?? [];
                    return (
                        RepositoryName: repositoryName,
                        Snapshots: snapshotRecords.Select(s => new ElasticsearchSnapshotResponse(
                            Repository: repositoryName,
                            Name: s.Id ?? String.Empty,
                            Status: s.Status ?? String.Empty,
                            StartTime: s.StartEpoch > 0 ? DateTimeOffset.FromUnixTimeSeconds(s.StartEpoch).UtcDateTime : null,
                            EndTime: s.EndEpoch > 0 ? DateTimeOffset.FromUnixTimeSeconds(s.EndEpoch).UtcDateTime : null,
                            Duration: s.Duration?.ToString() ?? String.Empty,
                            IndicesCount: s.Indices,
                            SuccessfulShards: s.SuccessfulShards,
                            FailedShards: s.FailedShards,
                            TotalShards: s.TotalShards
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

    [HiddenEndpoint]
    [HandlerAuthorize(Policies = [AuthorizationRoles.GlobalAdminPolicy])]
    [HandlerEndpoint(HandlerMethod.Post, "/api/v2/admin/generate-sample-events", EndpointFilters = [typeof(AutoValidationEndpointFilter)])]
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
