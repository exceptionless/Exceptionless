using Exceptionless.Core.Mail;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Foundatio.Extensions.Hosting.Startup;
using Foundatio.Jobs;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public class EnqueueProjectSmartThrottleOnThrottleApplied : IStartupAction
{
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly IMessageSubscriber _subscriber;
    private readonly ILogger _logger;

    public EnqueueProjectSmartThrottleOnThrottleApplied(IQueue<WorkItemData> workItemQueue, IMessageSubscriber subscriber, ILoggerFactory loggerFactory)
    {
        _workItemQueue = workItemQueue;
        _subscriber = subscriber;
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
                EventLimit = throttle.EventLimit
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

    public ProjectSmartThrottleWorkItemHandler(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IUserRepository userRepository, IMailer mailer, UsageService usageService, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _mailer = mailer;
        _usageService = usageService;
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var wi = context.GetData<ProjectSmartThrottleWorkItem>()!;
        Log.LogInformation("Received smart throttle notification for project: {ProjectId} in organization: {OrganizationId}", wi.ProjectId, wi.OrganizationId);

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

            Log.LogTrace("Sending smart throttle email to {EmailAddress}...", user.EmailAddress);
            await _mailer.SendProjectThrottledNoticeAsync(user, organization, project, wi.SampleRate, currentProjectUsage, fairShareLimit);
        }

        Log.LogTrace("Done sending smart throttle emails");
    }
}
