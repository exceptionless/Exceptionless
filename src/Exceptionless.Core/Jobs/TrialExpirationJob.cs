using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Resilience;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

/// <summary>
/// Sends the trial ending reminder and moves organizations whose no-card trial has ended to the free plan.
/// </summary>
[Job(Description = "Sends trial reminders and moves expired trials to the free plan.", IsContinuous = false)]
public class TrialExpirationJob : JobWithLockBase, IHealthCheck
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;
    private readonly BillingOptions _billingOptions;
    private readonly IMailer _mailer;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILockProvider _lockProvider;
    private DateTime? _lastRun;

    public TrialExpirationJob(IOrganizationRepository organizationRepository, IUserRepository userRepository,
        BillingManager billingManager, BillingPlans plans, AppOptions appOptions, IMailer mailer,
        IMessagePublisher messagePublisher, ILockProvider lockProvider,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory
    ) : base(timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _billingManager = billingManager;
        _plans = plans;
        _billingOptions = appOptions.BillingOptions;
        _mailer = mailer;
        _messagePublisher = messagePublisher;
        _lockProvider = lockProvider;
    }

    protected override Task<ILock?> GetLockAsync(CancellationToken cancellationToken = default)
    {
        return _lockProvider.TryAcquireAsync(nameof(TrialExpirationJob), TimeSpan.FromMinutes(30), cancellationToken);
    }

    protected override async Task<JobResult> RunInternalAsync(JobContext context)
    {
        _lastRun = _timeProvider.GetUtcNow().UtcDateTime;

        var results = await _organizationRepository.FindAsync(q => q
            .FieldEquals(o => o.PlanId, _plans.TrialPlan.Id)
            .OnlyIds(), o => o.SearchAfterPaging().PageLimit(100));

        while (results.Hits.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            foreach (string organizationId in results.Hits.Where(h => h.Id is not null).Select(h => h.Id!))
            {
                try
                {
                    await ProcessOrganizationAsync(organizationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing trial for organization {OrganizationId}: {Message}", organizationId, ex.Message);
                }
            }

            if (!await results.NextPageAsync())
                break;

            await context.RenewLockAsync();
        }

        return JobResult.Success;
    }

    private async Task ProcessOrganizationAsync(string organizationId)
    {
        await using var billingLock = await _billingManager.AcquireOrganizationLockAsync(organizationId);
        var organization = await _organizationRepository.GetByIdAsync(organizationId, o => o.Cache(false));
        if (organization is null || !_plans.IsTrialPlan(organization.PlanId))
            return;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (organization.TrialEndsUtc is null || organization.TrialEndsUtc <= utcNow)
        {
            _logger.LogInformation("Trial ended for organization {OrganizationId}, moving to the free plan", organization.Id);
            _billingManager.EndTrial(organization);
            await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache().Originals());
            await _messagePublisher.PublishAsync(new PlanChanged { OrganizationId = organization.Id });
            return;
        }

        if (organization.TrialReminderSentUtc is not null || organization.TrialEndsUtc.Value.AddDays(-_billingOptions.TrialReminderDays) > utcNow)
            return;

        // Mark the reminder before sending so a failure can't cause repeated emails.
        organization.TrialReminderSentUtc = utcNow;
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache().Originals());

        var users = await _userRepository.GetByOrganizationIdAsync(organization.Id);
        foreach (var user in users.Documents.Where(u => u.IsEmailAddressVerified && u.EmailNotificationsEnabled))
            await _mailer.SendOrganizationTrialEndingAsync(user, organization);

        _logger.LogInformation("Sent trial ending reminder for organization {OrganizationId}", organization.Id);
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_lastRun.HasValue)
            return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

        if (_timeProvider.GetUtcNow().UtcDateTime.Subtract(_lastRun.Value) > TimeSpan.FromMinutes(65))
            return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last 65 minutes."));

        return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last 65 minutes."));
    }
}
