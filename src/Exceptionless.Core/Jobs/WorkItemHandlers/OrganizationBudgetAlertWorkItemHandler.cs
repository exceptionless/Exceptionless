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

public class EnqueueOrganizationBudgetAlertOnUsageThreshold : IStartupAction
{
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly IMessageSubscriber _subscriber;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public EnqueueOrganizationBudgetAlertOnUsageThreshold(IQueue<WorkItemData> workItemQueue, IMessageSubscriber subscriber, TimeProvider timeProvider, ILoggerFactory loggerFactory)
    {
        _workItemQueue = workItemQueue;
        _subscriber = subscriber;
        _timeProvider = timeProvider;
        _logger = loggerFactory.CreateLogger<EnqueueOrganizationBudgetAlertOnUsageThreshold>();
    }

    public Task RunAsync(CancellationToken shutdownToken = default)
    {
        return _subscriber.SubscribeAsync<OrganizationBudgetAlert>(async alert =>
        {
            _logger.LogInformation("Enqueueing budget alert work item for organization: {OrganizationId} Threshold: {Threshold}%", alert.OrganizationId, alert.Threshold);

            await _workItemQueue.EnqueueAsync(new OrganizationBudgetAlertWorkItem
            {
                OrganizationId = alert.OrganizationId,
                Threshold = alert.Threshold,
                ThresholdEventCount = alert.ThresholdEventCount,
                CurrentEventCount = alert.CurrentEventCount,
                EventLimit = alert.EventLimit,
                UsagePeriod = alert.UsagePeriod > 0 ? alert.UsagePeriod : _timeProvider.GetUtcNow().UtcDateTime.StartOfMonth().ToEpoch()
            });
        }, shutdownToken);
    }
}

public class OrganizationBudgetAlertWorkItemHandler : WorkItemHandlerBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMailer _mailer;
    private readonly UsageService _usageService;
    private readonly NotificationService _notificationService;
    private readonly TimeProvider _timeProvider;

    public OrganizationBudgetAlertWorkItemHandler(IOrganizationRepository organizationRepository, IUserRepository userRepository, IMailer mailer, UsageService usageService,
        NotificationService notificationService, TimeProvider timeProvider, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _mailer = mailer;
        _usageService = usageService;
        _notificationService = notificationService;
        _timeProvider = timeProvider;
    }

    public override Task<ILock?> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = default)
    {
        var wi = (OrganizationBudgetAlertWorkItem)workItem;
        int currentUsagePeriod = _timeProvider.GetUtcNow().UtcDateTime.StartOfMonth().ToEpoch();
        return _notificationService.TryAcquireUsageNotificationLockAsync(wi.GetUniqueIdentifier(currentUsagePeriod));
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var wi = context.GetData<OrganizationBudgetAlertWorkItem>()!;
        Log.LogInformation("Received budget alert work item for organization: {OrganizationId} Threshold: {Threshold}%", wi.OrganizationId, wi.Threshold);

        int usagePeriod = wi.UsagePeriod > 0 ? wi.UsagePeriod : _timeProvider.GetUtcNow().UtcDateTime.StartOfMonth().ToEpoch();
        string notificationIdentifier = wi.GetUniqueIdentifier(usagePeriod);
        if (usagePeriod != _timeProvider.GetUtcNow().UtcDateTime.StartOfMonth().ToEpoch())
        {
            Log.LogInformation("Budget alert period {UsagePeriod} is stale for organization {OrganizationId}, skipping", wi.UsagePeriod, wi.OrganizationId);
            return;
        }

        var organization = await _organizationRepository.GetByIdAsync(wi.OrganizationId, o => o.Cache());
        if (organization is null)
        {
            Log.LogWarning("Organization {OrganizationId} not found, skipping budget alert", wi.OrganizationId);
            return;
        }

        // Re-check that budget alerts are still enabled and the threshold is still configured
        if (organization.BudgetAlertSettings is not { Enabled: true, Thresholds: not null } || !organization.BudgetAlertSettings.Thresholds.Contains(wi.Threshold))
        {
            Log.LogInformation("Budget alerts disabled or threshold {Threshold}% removed for organization: {OrganizationId}, skipping", wi.Threshold, wi.OrganizationId);
            return;
        }

        int eventLimit = await _usageService.GetMaxEventsPerMonthAsync(organization.Id);
        if (eventLimit <= 0)
        {
            Log.LogInformation("Organization {OrganizationId} no longer has a finite event allowance, skipping budget alert", wi.OrganizationId);
            return;
        }

        int thresholdEventCount = UsageService.GetBudgetThresholdEventCount(eventLimit, wi.Threshold);
        int currentEventCount = (await _usageService.GetUsageAsync(organization.Id)).CurrentUsage.Total;
        if (currentEventCount < thresholdEventCount)
        {
            Log.LogInformation("Organization {OrganizationId} usage is now below budget threshold {Threshold}%, skipping", wi.OrganizationId, wi.Threshold);
            return;
        }

        var results = await _userRepository.GetByOrganizationIdAsync(organization.Id);
        foreach (var user in results.Documents)
        {
            if (!user.IsEmailAddressVerified)
            {
                Log.LogInformation("User {UserId} with email address {EmailAddress} has not been verified, skipping budget alert", user.Id, user.EmailAddress);
                continue;
            }

            if (!user.EmailNotificationsEnabled)
            {
                Log.LogInformation("User {UserId} with email address {EmailAddress} has email notifications disabled, skipping budget alert", user.Id, user.EmailAddress);
                continue;
            }

            if (await _notificationService.IsUsageNotificationRecipientSentAsync(notificationIdentifier, user.Id))
                continue;

            await using var recipientLock = await _notificationService.TryAcquireUsageNotificationRecipientLockAsync(notificationIdentifier, user.Id);
            if (recipientLock is null || await _notificationService.IsUsageNotificationRecipientSentAsync(notificationIdentifier, user.Id))
                continue;

            Log.LogTrace("Sending budget alert email to {EmailAddress}...", user.EmailAddress);
            await _mailer.SendOrganizationBudgetAlertAsync(user, organization, wi.Threshold, thresholdEventCount, currentEventCount, eventLimit);
            await _notificationService.MarkUsageNotificationRecipientSentAsync(notificationIdentifier, user.Id, usagePeriod);
        }

        Log.LogTrace("Done sending budget alert emails");
    }
}
