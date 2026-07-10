using Exceptionless.Core.Extensions;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
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
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStackRepository _stackRepository;

    public RateNotificationsJob(
        IQueue<RateNotification> queue,
        IMailer mailer,
        IRateNotificationRuleRepository ruleRepository,
        IOrganizationRepository organizationRepository,
        IProjectRepository projectRepository,
        IUserRepository userRepository,
        IStackRepository stackRepository,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory) : base(queue, timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _mailer = mailer;
        _ruleRepository = ruleRepository;
        _organizationRepository = organizationRepository;
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
            return Skip($"Rate notification rule {wi.RuleId} not found; skipping.");

        if (!rule.IsEnabled)
            return Skip($"Rule {wi.RuleId} is disabled; skipping.");

        string expectedSubjectKey = rule.Subject == RateNotificationSubject.Stack
            ? $"stack:{rule.StackId}"
            : $"project:{rule.ProjectId}";
        if (!String.Equals(rule.OrganizationId, wi.OrganizationId, StringComparison.Ordinal) ||
            !String.Equals(rule.ProjectId, wi.ProjectId, StringComparison.Ordinal) ||
            !String.Equals(rule.UserId, wi.UserId, StringComparison.Ordinal) ||
            !String.Equals(rule.StackId, wi.StackId, StringComparison.Ordinal) ||
            !String.Equals(expectedSubjectKey, wi.SubjectKey, StringComparison.Ordinal) ||
            wi.Threshold != rule.Threshold || wi.ObservedCount < 0 || wi.WindowStartUtc >= wi.WindowEndUtc)
        {
            _logger.LogWarning("Rate notification payload does not match rule {RuleId}; skipping", wi.RuleId);
            return Skip($"Rate notification payload does not match rule {wi.RuleId}; skipping.");
        }

        // Version check — rule was mutated after enqueueing
        if (rule.Version != wi.RuleVersion)
        {
            _logger.LogInformation("Rule {RuleId} version mismatch: expected {Expected}, found {Actual}; skipping stale notification",
                wi.RuleId, wi.RuleVersion, rule.Version);
            return Skip($"Rate notification rule {wi.RuleId} changed after enqueue; skipping.");
        }

        var organization = await _organizationRepository.GetByIdAsync(rule.OrganizationId, o => o.Cache());
        if (organization is null || !organization.HasRateNotifications())
            return Skip($"Organization {rule.OrganizationId} cannot receive rate notifications; skipping.");

        var project = await _projectRepository.GetByIdAsync(rule.ProjectId, o => o.Cache());
        if (project is null || !String.Equals(project.OrganizationId, rule.OrganizationId, StringComparison.Ordinal))
            return Skip($"Project {rule.ProjectId} not found; skipping.");

        // Load user
        var user = await _userRepository.GetByIdAsync(rule.UserId, o => o.Cache());
        if (user is null)
            return Skip($"User {rule.UserId} not found; skipping.");

        // User must still be a member of the organization
        if (!user.OrganizationIds.Contains(rule.OrganizationId))
        {
            _logger.LogInformation("User {UserId} is no longer a member of organization {OrganizationId}; skipping rate notification", rule.UserId, rule.OrganizationId);
            return Skip($"User {rule.UserId} is no longer a member of organization {rule.OrganizationId}; skipping.");
        }

        if (!user.IsEmailAddressVerified)
        {
            _logger.LogInformation("User {UserId} email not verified; skipping rate notification", rule.UserId);
            return Skip($"User {rule.UserId} email is not verified; skipping.");
        }

        if (!user.EmailNotificationsEnabled)
        {
            _logger.LogInformation("User {UserId} has email notifications disabled; skipping rate notification", rule.UserId);
            return Skip($"User {rule.UserId} disabled email notifications; skipping.");
        }

        // Load stack if this is a stack-scoped rule
        Stack? stack = null;
        if (rule.Subject == RateNotificationSubject.Stack && !String.IsNullOrEmpty(rule.StackId))
        {
            stack = await _stackRepository.GetByIdAsync(rule.StackId, o => o.Cache());
            if (stack is null ||
                !String.Equals(stack.ProjectId, rule.ProjectId, StringComparison.Ordinal) ||
                !String.Equals(stack.OrganizationId, rule.OrganizationId, StringComparison.Ordinal) ||
                !stack.AllowNotifications)
            {
                return Skip($"Stack {rule.StackId} cannot receive rate notifications; skipping.");
            }
        }

        await _mailer.SendRateNotificationAsync(user, project, rule, wi.ObservedCount, wi.WindowStartUtc, wi.WindowEndUtc, stack);
        AppDiagnostics.RateNotificationsSent.Add(1);

        _logger.LogInformation("Sent rate notification email: rule={RuleId} user={UserId} project={ProjectId} observed={Observed}",
            rule.Id, rule.UserId, rule.ProjectId, wi.ObservedCount);

        return JobResult.Success;
    }

    private static JobResult Skip(string message)
    {
        AppDiagnostics.RateNotificationsSkipped.Add(1);
        return JobResult.SuccessWithMessage(message);
    }
}
