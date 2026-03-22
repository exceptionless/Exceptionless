using Exceptionless.Core;
using Exceptionless.Core.Authorization;
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
using Exceptionless.Web.Extensions;
using Exceptionless.Web.Models.Admin;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Repositories.Migrations;
using Foundatio.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Controllers;

[Route(API_PREFIX + "/admin")]
[Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
[ApiExplorerSettings(IgnoreApi = true)]
public class AdminController : ExceptionlessApiController
{
    private readonly ILogger _logger;
    private readonly ExceptionlessElasticConfiguration _configuration;
    private readonly IFileStorage _fileStorage;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IUserRepository _userRepository;
    private readonly IQueue<EventPost> _eventPostQueue;
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly AppOptions _appOptions;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;
    private readonly IMigrationStateRepository _migrationStateRepository;

    public AdminController(
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
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory) : base(timeProvider)
    {
        _logger = loggerFactory.CreateLogger<AdminController>();
        _configuration = configuration;
        _fileStorage = fileStorage;
        _messagePublisher = messagePublisher;
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _stackRepository = stackRepository;
        _eventRepository = eventRepository;
        _userRepository = userRepository;
        _eventPostQueue = eventPostQueue;
        _workItemQueue = workItemQueue;
        _appOptions = appOptions;
        _billingManager = billingManager;
        _plans = plans;
        _migrationStateRepository = migrationStateRepository;
    }

    [HttpGet("settings")]
    public ActionResult SettingsRequest()
    {
        return Ok(_appOptions);
    }

    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsResponse>> GetStatsAsync()
    {
        var organizationCountTask = _organizationRepository.CountAsync(q => q
            .AggregationsExpression("terms:billing_status date:created_utc~1M"));

        var userCountTask = _userRepository.CountAsync();
        var projectCountTask = _projectRepository.CountAsync();

        var stackCountTask = _stackRepository.CountAsync(q => q
            .AggregationsExpression("terms:status terms:(type terms:status)"));

        var eventCountTask = _eventRepository.CountAsync(q => q
            .AggregationsExpression("date:date~1M"));

        await Task.WhenAll(organizationCountTask, userCountTask, projectCountTask, stackCountTask, eventCountTask);

        return Ok(new AdminStatsResponse(
            Organizations: await organizationCountTask,
            Users: await userCountTask,
            Projects: await projectCountTask,
            Stacks: await stackCountTask,
            Events: await eventCountTask
        ));
    }

    [HttpGet("migrations")]
    public async Task<ActionResult<MigrationsResponse>> GetMigrationsAsync()
    {
        var result = await _migrationStateRepository.GetAllAsync(o => o.SearchAfterPaging().PageLimit(1000));
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

        return Ok(new MigrationsResponse(currentVersion, states));
    }

    [HttpGet("echo")]
    public ActionResult EchoRequest()
    {
        return Ok(new
        {
            Request.Headers,
            IpAddress = Request.GetClientIpAddress()
        });
    }

    [HttpGet("assemblies")]
    public ActionResult<IEnumerable<AssemblyDetail>> Assemblies()
    {
        var details = AssemblyDetail.ExtractAll();
        return Ok(details);
    }

    [HttpPost("change-plan")]
    public async Task<IActionResult> ChangePlanAsync(string organizationId, string planId)
    {
        if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
            return Ok(new { Success = false, Message = "Invalid Organization Id." });

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization is null)
            return Ok(new { Success = false, Message = "Invalid Organization Id." });

        var plan = _billingManager.GetBillingPlan(planId);
        if (plan is null)
            return Ok(new { Success = false, Message = "Invalid PlanId." });

        organization.BillingStatus = !String.Equals(plan.Id, _plans.FreePlan.Id) ? BillingStatus.Active : BillingStatus.Trialing;
        organization.RemoveSuspension();
        _billingManager.ApplyBillingPlan(organization, plan, CurrentUser, false);

        await _organizationRepository.SaveAsync(organization, o => o.Cache().Originals());
        await _messagePublisher.PublishAsync(new PlanChanged
        {
            OrganizationId = organization.Id
        });

        return Ok(new { Success = true });
    }

    /// <summary>
    /// Applies a bonus event count to the specified organization, optionally with an expiration date.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization to receive the bonus.</param>
    /// <param name="bonusEvents">The number of bonus events to apply.</param>
    /// <param name="expires">The optional expiration date for the bonus events.</param>
    /// <response code="200">Bonus was applied successfully.</response>
    /// <response code="422">Validation error occurred.</response>
    [HttpPost("set-bonus")]
    public async Task<IActionResult> SetBonusAsync(string organizationId, int bonusEvents, DateTime? expires = null)
    {
        if (String.IsNullOrEmpty(organizationId) || !CanAccessOrganization(organizationId))
        {
            ModelState.AddModelError(nameof(organizationId), "Invalid Organization Id");
            return ValidationProblem(ModelState);
        }

        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization is null)
        {
            ModelState.AddModelError(nameof(organizationId), "Invalid Organization Id");
            return ValidationProblem(ModelState);
        }

        _billingManager.ApplyBonus(organization, bonusEvents, expires);
        await _organizationRepository.SaveAsync(organization, o => o.Cache().Originals());

        return Ok();
    }

    [HttpGet("requeue")]
    public async Task<IActionResult> RequeueAsync(string? path = null, bool archive = false)
    {
        if (String.IsNullOrEmpty(path))
            path = @"q\*";

        int enqueued = 0;
        foreach (var file in await _fileStorage.GetFileListAsync(path))
        {
            await _eventPostQueue.EnqueueAsync(new EventPost(_appOptions.EnableArchive && archive) { FilePath = file.Path });
            enqueued++;
        }

        return Ok(new { Enqueued = enqueued });
    }

    [HttpGet("maintenance/{name:minlength(1)}")]
    public async Task<IActionResult> RunJobAsync(string name, DateTime? utcStart = null, DateTime? utcEnd = null, string? organizationId = null)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        switch (name.ToLowerInvariant())
        {
            case "fix-stack-stats":
                var effectiveUtcStart = utcStart ?? _timeProvider.GetUtcNow().UtcDateTime.AddDays(-90);

                if (utcEnd.HasValue && utcEnd.Value.IsBefore(effectiveUtcStart))
                {
                    ModelState.AddModelError(nameof(utcEnd), "utcEnd must be greater than or equal to utcStart.");
                    return ValidationProblem(ModelState);
                }

                await _workItemQueue.EnqueueAsync(new FixStackStatsWorkItem
                {
                    UtcStart = effectiveUtcStart,
                    UtcEnd = utcEnd,
                    OrganizationId = organizationId
                });
                break;
            case "increment-project-configuration-version":
                await _workItemQueue.EnqueueAsync(new ProjectMaintenanceWorkItem { IncrementConfigurationVersion = true });
                break;
            case "indexes":
                if (!_appOptions.ElasticsearchOptions.DisableIndexConfiguration)
                    await _configuration.ConfigureIndexesAsync(beginReindexingOutdated: false);
                break;
            case "normalize-user-email-address":
                await _workItemQueue.EnqueueAsync(new UserMaintenanceWorkItem { Normalize = true });
                break;
            case "remove-old-organization-usage":
                await _workItemQueue.EnqueueAsync(new OrganizationMaintenanceWorkItem { RemoveOldUsageStats = true });
                break;
            case "remove-old-project-usage":
                await _workItemQueue.EnqueueAsync(new ProjectMaintenanceWorkItem { RemoveOldUsageStats = true });
                break;
            case "reset-verify-email-address-token-and-expiration":
                await _workItemQueue.EnqueueAsync(new UserMaintenanceWorkItem { ResetVerifyEmailAddressToken = true });
                break;
            case "update-organization-plans":
                await _workItemQueue.EnqueueAsync(new OrganizationMaintenanceWorkItem { UpgradePlans = true });
                break;
            case "update-project-default-bot-lists":
                await _workItemQueue.EnqueueAsync(new ProjectMaintenanceWorkItem { UpdateDefaultBotList = true, IncrementConfigurationVersion = true });
                break;
            case "update-project-notification-settings":
                await _workItemQueue.EnqueueAsync(new UpdateProjectNotificationSettingsWorkItem
                {
                    OrganizationId = organizationId
                });
                break;
            default:
                return NotFound();
        }

        return Ok();
    }

    [HttpGet("elasticsearch")]
    public async Task<ActionResult<ElasticsearchInfoResponse>> GetElasticsearchInfoAsync()
    {
        var client = _configuration.Client;
        var healthTask = client.Cluster.HealthAsync(r => r.Level(Elastic.Clients.Elasticsearch.Level.Indices));
        var statsTask = client.Cluster.StatsAsync();
        var indicesStatsTask = client.Indices.StatsAsync();
        await Task.WhenAll(healthTask, statsTask, indicesStatsTask);

        var healthResponse = await healthTask;
        var statsResponse = await statsTask;
        var indicesStatsResponse = await indicesStatsTask;

        if (!healthResponse.IsValidResponse || !statsResponse.IsValidResponse || !indicesStatsResponse.IsValidResponse)
            return Problem(title: "Elasticsearch cluster information is unavailable.");

        // Count unassigned shards per index from health response
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

        return Ok(new ElasticsearchInfoResponse(
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
        ));
    }

    [HttpGet("elasticsearch/snapshots")]
    public async Task<ActionResult<ElasticsearchSnapshotsResponse>> GetElasticsearchSnapshotsAsync()
    {
        var client = _configuration.Client;
        try
        {
            var repositoryResponse = await client.Snapshot.GetRepositoryAsync();
            if (!repositoryResponse.IsValidResponse)
                return Problem(title: "Snapshot repository information is unavailable.");

            if (repositoryResponse.Repositories is null || !repositoryResponse.Repositories.Any())
                return Ok(new ElasticsearchSnapshotsResponse([], []));

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
                return Problem(title: "Unable to retrieve snapshot information.");

            var snapshots = successfulSnapshotResults
                .SelectMany(r => r.Snapshots)
                .OrderByDescending(s => s.StartTime)
                .ToArray();

            var successfulRepositoryNames = successfulSnapshotResults
                .Select(r => r.RepositoryName)
                .ToArray();

            return Ok(new ElasticsearchSnapshotsResponse(successfulRepositoryNames, snapshots));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to retrieve snapshot information");
            return Problem(title: "Unable to retrieve snapshot information.");
        }
    }
}

