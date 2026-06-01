using Exceptionless.Core.Mail;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public class EnqueueOrganizationBudgetAlertOnUsageThreshold : IStartupAction
{
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly IMessageSubscriber _subscriber;
    private readonly ILogger _logger;

    public EnqueueOrganizationBudgetAlertOnUsageThreshold(IQueue<WorkItemData> workItemQueue, IMessageSubscriber subscriber, ILoggerFactory loggerFactory)
    {
        _workItemQueue = workItemQueue;
        _subscriber = subscriber;
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
                EventLimit = alert.EventLimit
            });
        }, shutdownToken);
    }
}

public class OrganizationBudgetAlertWorkItemHandler : WorkItemHandlerBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IMailer _mailer;

    public OrganizationBudgetAlertWorkItemHandler(IOrganizationRepository organizationRepository, IUserRepository userRepository, IMailer mailer, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _mailer = mailer;
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var wi = context.GetData<OrganizationBudgetAlertWorkItem>()!;
        Log.LogInformation("Received budget alert work item for organization: {OrganizationId} Threshold: {Threshold}%", wi.OrganizationId, wi.Threshold);

        var organization = await _organizationRepository.GetByIdAsync(wi.OrganizationId, o => o.Cache());
        if (organization is null)
        {
            Log.LogWarning("Organization {OrganizationId} not found, skipping budget alert", wi.OrganizationId);
            return;
        }

        // Re-check that budget alerts are still enabled and the threshold is still configured
        if (organization.BudgetAlertSettings is not { Enabled: true } || !organization.BudgetAlertSettings.Thresholds.Contains(wi.Threshold))
        {
            Log.LogInformation("Budget alerts disabled or threshold {Threshold}% removed for organization: {OrganizationId}, skipping", wi.Threshold, wi.OrganizationId);
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

            Log.LogTrace("Sending budget alert email to {EmailAddress}...", user.EmailAddress);
            await _mailer.SendOrganizationBudgetAlertAsync(user, organization, wi.Threshold, wi.ThresholdEventCount, wi.CurrentEventCount, wi.EventLimit);
        }

        Log.LogTrace("Done sending budget alert emails");
    }
}
