using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Resilience;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

[Job(Description = "Delivers rate notification emails.", InitialDelay = "5s")]
public class RateNotificationsJob : QueueJobBase<RateNotification>
{
    private readonly IMailer _mailer;
    private readonly IRateNotificationRuleRepository _ruleRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStackRepository _stackRepository;

    public RateNotificationsJob(
        IQueue<RateNotification> queue,
        IMailer mailer,
        IRateNotificationRuleRepository ruleRepository,
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        IStackRepository stackRepository,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory) : base(queue, timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _mailer = mailer;
        _ruleRepository = ruleRepository;
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _stackRepository = stackRepository;
    }

    protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<RateNotification> context)
    {
        var wi = context.QueueEntry.Value;

        // Load rule
        var rule = await _ruleRepository.GetByIdAsync(wi.RuleId);
        if (rule is null)
            return JobResult.SuccessWithMessage($"Rate notification rule {wi.RuleId} not found; skipping.");

        if (!rule.IsEnabled)
            return JobResult.SuccessWithMessage($"Rule {wi.RuleId} is disabled; skipping.");

        // Version check — rule was mutated after enqueueing
        if (rule.Version != wi.RuleVersion)
        {
            _logger.LogInformation("Rule {RuleId} version mismatch: expected {Expected}, found {Actual}; skipping stale notification",
                wi.RuleId, wi.RuleVersion, rule.Version);
            return JobResult.Success;
        }

        // Load project
        var project = await _projectRepository.GetByIdAsync(rule.ProjectId, o => o.Cache());
        if (project is null)
            return JobResult.SuccessWithMessage($"Project {rule.ProjectId} not found; skipping.");

        // Load user
        var user = await _userRepository.GetByIdAsync(rule.UserId, o => o.Cache());
        if (user is null)
            return JobResult.SuccessWithMessage($"User {rule.UserId} not found; skipping.");

        // User must still be a member of the org
        if (!user.OrganizationIds.Contains(rule.OrganizationId))
        {
            _logger.LogInformation("User {UserId} is no longer a member of org {OrgId}; skipping rate notification", rule.UserId, rule.OrganizationId);
            return JobResult.Success;
        }

        if (!user.IsEmailAddressVerified)
        {
            _logger.LogInformation("User {UserId} email not verified; skipping rate notification", rule.UserId);
            return JobResult.Success;
        }

        if (!user.EmailNotificationsEnabled)
        {
            _logger.LogInformation("User {UserId} has email notifications disabled; skipping rate notification", rule.UserId);
            return JobResult.Success;
        }

        // Load stack if this is a stack-scoped rule
        Stack? stack = null;
        if (rule.Subject == RateNotificationSubject.Stack && !String.IsNullOrEmpty(rule.StackId))
        {
            stack = await _stackRepository.GetByIdAsync(rule.StackId, o => o.Cache());
            if (stack is null)
                _logger.LogWarning("Stack {StackId} not found for rate notification rule {RuleId}", rule.StackId, rule.Id);
        }

        await _mailer.SendRateNotificationAsync(user, project, rule, wi.ObservedCount, wi.WindowStartUtc, wi.WindowEndUtc, stack);

        _logger.LogInformation("Sent rate notification email: rule={RuleId} user={UserId} project={ProjectId} observed={Observed}",
            rule.Id, rule.UserId, rule.ProjectId, wi.ObservedCount);

        return JobResult.Success;
    }
}
