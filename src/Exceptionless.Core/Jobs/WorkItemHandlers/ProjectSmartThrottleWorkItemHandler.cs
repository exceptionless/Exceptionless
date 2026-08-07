using Exceptionless.Core.Mail;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public class EnqueueProjectSmartThrottleOnThrottleApplied : IStartupAction
{
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly IMessageSubscriber _subscriber;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public EnqueueProjectSmartThrottleOnThrottleApplied(IQueue<WorkItemData> workItemQueue, IMessageSubscriber subscriber, TimeProvider timeProvider, ILoggerFactory loggerFactory)
    {
        _workItemQueue = workItemQueue;
        _subscriber = subscriber;
        _timeProvider = timeProvider;
        _logger = loggerFactory.CreateLogger<EnqueueProjectSmartThrottleOnThrottleApplied>();
    }

    public Task RunAsync(CancellationToken shutdownToken = default)
    {
        return _subscriber.SubscribeAsync<ProjectSmartThrottleApplied>(async throttle =>
        {
            _logger.LogInformation("Enqueueing smart throttle notification for project: {ProjectId} in organization: {OrganizationId}", throttle.ProjectId, throttle.OrganizationId);

            await _workItemQueue.EnqueueAsync(new ProjectSmartThrottleWorkItem
            {
                OrganizationId = throttle.OrganizationId,
                ProjectId = throttle.ProjectId,
                SampleRate = throttle.SampleRate,
                CurrentEventCount = throttle.CurrentEventCount,
                EventLimit = throttle.EventLimit,
                UsagePeriod = throttle.UsagePeriod > 0 ? throttle.UsagePeriod : _timeProvider.GetUtcNow().UtcDateTime.StartOfMonth().ToEpoch()
            });
        }, shutdownToken);
    }
}

public class ProjectSmartThrottleWorkItemHandler : WorkItemHandlerBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMailer _mailer;
    private readonly UsageService _usageService;
    private readonly NotificationService _notificationService;
    private readonly TimeProvider _timeProvider;

    public ProjectSmartThrottleWorkItemHandler(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IUserRepository userRepository, IMailer mailer, UsageService usageService,
        NotificationService notificationService, TimeProvider timeProvider, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _mailer = mailer;
        _usageService = usageService;
        _notificationService = notificationService;
        _timeProvider = timeProvider;
    }

    public override Task<ILock?> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = default)
    {
        var wi = (ProjectSmartThrottleWorkItem)workItem;
        int currentUsagePeriod = _timeProvider.GetUtcNow().UtcDateTime.StartOfMonth().ToEpoch();
        return _notificationService.TryAcquireUsageNotificationLockAsync(wi.GetUniqueIdentifier(currentUsagePeriod));
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var wi = context.GetData<ProjectSmartThrottleWorkItem>()!;
        Log.LogInformation("Received smart throttle notification for project: {ProjectId} in organization: {OrganizationId}", wi.ProjectId, wi.OrganizationId);

        int usagePeriod = wi.UsagePeriod > 0 ? wi.UsagePeriod : _timeProvider.GetUtcNow().UtcDateTime.StartOfMonth().ToEpoch();
        string notificationIdentifier = wi.GetUniqueIdentifier(usagePeriod);
        if (usagePeriod != _timeProvider.GetUtcNow().UtcDateTime.StartOfMonth().ToEpoch())
        {
            Log.LogInformation("Smart throttle period {UsagePeriod} is stale for project {ProjectId}, skipping", wi.UsagePeriod, wi.ProjectId);
            return;
        }

        var organization = await _organizationRepository.GetByIdAsync(wi.OrganizationId, o => o.Cache());
        if (organization is null)
        {
            Log.LogWarning("Organization {OrganizationId} not found, skipping smart throttle notification", wi.OrganizationId);
            return;
        }

        var project = await _projectRepository.GetByIdAsync(wi.ProjectId, o => o.Cache());
        if (project is null)
        {
            Log.LogWarning("Project {ProjectId} not found, skipping smart throttle notification", wi.ProjectId);
            return;
        }

        if (!String.Equals(project.OrganizationId, organization.Id, StringComparison.Ordinal) ||
            !await _usageService.IsProjectSmartThrottledAsync(organization.Id, project.Id))
        {
            Log.LogInformation("Project {ProjectId} is no longer smart throttled, skipping notification", wi.ProjectId);
            return;
        }

        int organizationLimit = await _usageService.GetMaxEventsPerMonthAsync(organization.Id);
        if (organizationLimit <= 0)
        {
            Log.LogInformation("Organization {OrganizationId} no longer has a finite event allowance, skipping smart throttle notification", wi.OrganizationId);
            return;
        }

        int projectCount = (int)Math.Max(1, (await _projectRepository.GetCountByOrganizationIdAsync(organization.Id)).Total);
        int fairShareLimit = organizationLimit / projectCount;
        int currentProjectUsage = (await _usageService.GetUsageAsync(organization.Id, project.Id)).CurrentUsage.Total;

        var results = await _userRepository.GetByOrganizationIdAsync(organization.Id);
        foreach (var user in results.Documents)
        {
            if (!user.IsEmailAddressVerified)
            {
                Log.LogInformation("User {UserId} with email address {EmailAddress} has not been verified, skipping throttle notification", user.Id, user.EmailAddress);
                continue;
            }

            if (!user.EmailNotificationsEnabled)
            {
                Log.LogInformation("User {UserId} with email address {EmailAddress} has email notifications disabled, skipping throttle notification", user.Id, user.EmailAddress);
                continue;
            }

            if (await _notificationService.IsUsageNotificationRecipientSentAsync(notificationIdentifier, user.Id))
                continue;

            await using var recipientLock = await _notificationService.TryAcquireUsageNotificationRecipientLockAsync(notificationIdentifier, user.Id);
            if (recipientLock is null || await _notificationService.IsUsageNotificationRecipientSentAsync(notificationIdentifier, user.Id))
                continue;

            Log.LogTrace("Sending smart throttle email to {EmailAddress}...", user.EmailAddress);
            await _mailer.SendProjectThrottledNoticeAsync(user, organization, project, wi.SampleRate, currentProjectUsage, fairShareLimit);
            await _notificationService.MarkUsageNotificationRecipientSentAsync(notificationIdentifier, user.Id, usagePeriod);
        }

        Log.LogTrace("Done sending smart throttle emails");
    }
}
