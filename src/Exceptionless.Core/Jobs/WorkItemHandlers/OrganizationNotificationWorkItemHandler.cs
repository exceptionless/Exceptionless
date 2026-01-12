using Exceptionless.Core.Mail;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Resilience;
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

public class OrganizationNotificationWorkItemHandler : WorkItemHandlerBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMailer _mailer;
    private readonly ILockProvider _lockProvider;

    public OrganizationNotificationWorkItemHandler(IOrganizationRepository organizationRepository, IUserRepository userRepository, IMailer mailer, ICacheClient cacheClient, TimeProvider timeProvider, IResiliencePolicyProvider resiliencePolicyProvider, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _mailer = mailer;
        _lockProvider = new ThrottlingLockProvider(cacheClient, 1, TimeSpan.FromHours(1), timeProvider, resiliencePolicyProvider, loggerFactory);
    }

    public override Task HandleItemAsync(WorkItemContext context)
    {
        var wi = context.GetData<OrganizationNotificationWorkItem>();
        string cacheKey = $"{nameof(OrganizationNotificationWorkItemHandler)}:{wi.OrganizationId}";

        return _lockProvider.TryUsingAsync(cacheKey, async () =>
        {
            Log.LogInformation("Received organization notification work item for: {Organization} IsOverHourlyLimit: {IsOverHourlyLimit} IsOverMonthlyLimit: {IsOverMonthlyLimit}", wi.OrganizationId, wi.IsOverHourlyLimit, wi.IsOverMonthlyLimit);
            var organization = await _organizationRepository.GetByIdAsync(wi.OrganizationId, o => o.Cache());
            if (organization is null)
                return;

            if (wi.IsOverMonthlyLimit)
                await SendOverageNotificationsAsync(organization, wi.IsOverHourlyLimit, wi.IsOverMonthlyLimit);
        }, TimeSpan.FromMinutes(15), new CancellationToken(true));
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
}
