using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public class EnqueueOrganizationNotificationOnPlanOverage : IStartupAction
{
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly IMessageSubscriber _subscriber;
    private readonly ILogger _logger;

    public EnqueueOrganizationNotificationOnPlanOverage(IQueue<WorkItemData> workItemQueue, IMessageSubscriber subscriber, ILoggerFactory loggerFactory)
    {
        _workItemQueue = workItemQueue;
        _subscriber = subscriber;
        _logger = loggerFactory.CreateLogger<EnqueueOrganizationNotificationOnPlanOverage>();
    }

    public Task RunAsync(CancellationToken shutdownToken = default)
    {
        return _subscriber.SubscribeAsync<PlanOverage>(async overage =>
        {
            _logger.LogInformation("Enqueueing plan overage work item for organization: {OrganizationId} IsOverHourlyLimit: {IsOverHourlyLimit} IsOverMonthlyLimit: {IsOverMonthlyLimit}", overage.OrganizationId, overage.IsHourly, !overage.IsHourly);

            await _workItemQueue.EnqueueAsync(new OrganizationNotificationWorkItem
            {
                OrganizationId = overage.OrganizationId,
                IsOverHourlyLimit = overage.IsHourly,
                IsOverMonthlyLimit = !overage.IsHourly
            });
        }, shutdownToken);
    }
}

/// <summary>
/// Handles <see cref="OrganizationNotificationWorkItem"/> by sending a plan-overage email at most
/// once per overage state per organization.
///
/// <b>Root cause of duplicate emails (fixed here):</b>
/// Every web pod registers the same <see cref="EnqueueOrganizationNotificationOnPlanOverage"/>
/// startup action, so a single <c>PlanOverage</c> message fanned out to all pods and each pod
/// enqueued its own copy of the work item. The previous
/// <c>ThrottlingLockProvider(slotsPerPeriod: 1, period: 1 hour)</c> allowed exactly one item
/// through per calendar-hour bucket. Abandoned duplicates were re-queued and reprocessed once
/// each new bucket opened — producing one email per hour for as many duplicate items existed.
///
/// <b>Fix layers (both required):</b>
/// <list type="number">
///   <item><description>
///     Queue-level dedup: <see cref="OrganizationNotificationWorkItem.UniqueIdentifier"/> plus
///     <c>DuplicateDetectionQueueBehavior</c> collapses identical fanout enqueues to a single
///     queue entry, preventing duplicates from being enqueued in the first place.
///   </description></item>
///   <item><description>
///     Handler-level idempotency: a distributed lock serializes concurrent processing, and a
///     sent marker (<see cref="GetNotificationSentKey"/>) ensures that any stale duplicates
///     already in the queue before the fix deployed cannot re-trigger an email in the same UTC
///     month. The marker is reset when a monthly plan limit change re-evaluates the overage state.
///   </description></item>
/// </list>
///
/// Only monthly overages send email. Hourly items use the base work-item lock and return from
/// <see cref="HandleItemAsync"/> before touching the monthly notification lock/sent-key path, so
/// they can never block or suppress a later monthly notification.
/// Monthly items also re-check the current organization usage before sending so delayed queue
/// entries do not email after a plan or usage-state change leaves the organization under limit.
/// </summary>
public class OrganizationNotificationWorkItemHandler : WorkItemHandlerBase
{
    private static readonly TimeSpan WorkItemLockTimeout = TimeSpan.FromMinutes(65);

    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMailer _mailer;
    private readonly ICacheClient _cacheClient;
    private readonly TimeProvider _timeProvider;

    // ILockProvider is kept local rather than pushed to WorkItemHandlerBase because the
    // lock/sent-key contract is specific to plan-limit email idempotency. Passing it through
    // the base class would force every unrelated handler to acquire unnecessary plan-limit locks.
    private readonly ILockProvider _lockProvider;

    public OrganizationNotificationWorkItemHandler(IOrganizationRepository organizationRepository, IUserRepository userRepository, IMailer mailer, ICacheClient cacheClient, TimeProvider timeProvider, ILockProvider lockProvider, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _mailer = mailer;
        _cacheClient = cacheClient;
        _timeProvider = timeProvider;
        _lockProvider = lockProvider;
    }

    public override Task<ILock?> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = default)
    {
        var wi = (OrganizationNotificationWorkItem)workItem;

        // Hourly overages do not send email. Return the base EmptyLock so WorkItemJob calls
        // HandleItemAsync and then marks the queue entry as completed. Returning null instead
        // would cause WorkItemJob to call AbandonAsync, creating an infinite retry loop where
        // the item is re-queued and abandoned on every dequeue attempt.
        if (!ShouldSendNotificationEmail(wi))
            return base.GetWorkItemLockAsync(workItem, cancellationToken);

        // timeUntilExpires: exceed the 1-hour work-item queue timeout so a slow send does not let
        // a duplicate worker acquire the notification lock at the queue visibility boundary.
        //
        // acquireTimeout: TimeSpan.Zero — if another worker already holds the lock, return null
        // immediately so WorkItemJob calls AbandonAsync. The item is retried later, at which point
        // the sent marker is already set and the handler skips. Blocking here instead would stall
        // a worker slot for up to the lock timeout with no correctness benefit.
        return _lockProvider.TryAcquireAsync(GetNotificationLockKey(wi.OrganizationId, wi.IsOverMonthlyLimit), WorkItemLockTimeout, TimeSpan.Zero);
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var wi = context.GetData<OrganizationNotificationWorkItem>()!;
        if (!ShouldSendNotificationEmail(wi))
            return;

        Log.LogInformation("Received organization notification work item for: {OrganizationId} IsOverHourlyLimit: {IsOverHourlyLimit} IsOverMonthlyLimit: {IsOverMonthlyLimit}", wi.OrganizationId, wi.IsOverHourlyLimit, wi.IsOverMonthlyLimit);

        var organization = await _organizationRepository.GetByIdAsync(wi.OrganizationId, o => o.Cache());
        if (organization is null)
            return;

        if (!organization.IsOverMonthlyLimit(_timeProvider))
        {
            Log.LogInformation("Skipping stale monthly overage notification for organization: {OrganizationId} because it is no longer over the monthly limit", wi.OrganizationId);
            return;
        }

        if (await WasNotificationSentAsync(wi.OrganizationId, wi.IsOverMonthlyLimit))
        {
            Log.LogInformation("Skipping duplicate monthly overage notification for organization: {OrganizationId}", wi.OrganizationId);
            return;
        }

        await SendOverageNotificationsAsync(organization, wi.IsOverHourlyLimit, wi.IsOverMonthlyLimit);
        await _cacheClient.SetAsync(GetNotificationSentKey(wi.OrganizationId, wi.IsOverMonthlyLimit), true, GetNotificationSentExpiresAtUtc());
    }

    private async Task SendOverageNotificationsAsync(Organization organization, bool isOverHourlyLimit, bool isOverMonthlyLimit)
    {
        var results = await _userRepository.GetByOrganizationIdAsync(organization.Id);
        foreach (var user in results.Documents)
        {
            if (!user.IsEmailAddressVerified)
            {
                Log.LogInformation("User {UserId} with email address {EmailAddress} has not been verified", user.Id, user.EmailAddress);
                continue;
            }

            if (!user.EmailNotificationsEnabled)
            {
                Log.LogInformation("User {UserId} with email address {EmailAddress} has email notifications disabled", user.Id, user.EmailAddress);
                continue;
            }

            Log.LogTrace("Sending email to {EmailAddress}...", user.EmailAddress);
            await _mailer.SendOrganizationNoticeAsync(user, organization, isOverMonthlyLimit, isOverHourlyLimit);
        }

        Log.LogTrace("Done sending email");
    }

    // Only monthly overages send email. Hourly overages still trigger real-time websocket
    // budget updates in the UI but do not warrant a separate notification email.
    // Keeping hourly items out of the lock/sent-key path also means a burst of hourly events
    // cannot suppress the monthly notification that follows.
    private static bool ShouldSendNotificationEmail(OrganizationNotificationWorkItem workItem)
    {
        return workItem.IsOverMonthlyLimit;
    }

    private async Task<bool> WasNotificationSentAsync(string organizationId, bool isOverMonthlyLimit)
    {
        var sent = await _cacheClient.GetAsync<bool>(GetNotificationSentKey(organizationId, isOverMonthlyLimit));
        return sent.HasValue && sent.Value;
    }

    private DateTime GetNotificationSentExpiresAtUtc()
    {
        return _timeProvider.GetUtcNow().UtcDateTime.EndOfMonth();
    }

    public static string GetNotificationLockKey(string organizationId, bool isOverMonthlyLimit)
    {
        return $"{OrganizationNotificationWorkItem.GetNotificationKey(organizationId, isOverMonthlyLimit)}-lock";
    }

    public static string GetNotificationSentKey(string organizationId, bool isOverMonthlyLimit)
    {
        return $"{OrganizationNotificationWorkItem.GetNotificationKey(organizationId, isOverMonthlyLimit)}-sent";
    }
}
