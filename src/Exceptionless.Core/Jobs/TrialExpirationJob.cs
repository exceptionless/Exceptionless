using Exceptionless.Core.Billing;
using Exceptionless.Core.Configuration;
using Exceptionless.Core.Mail;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Resilience;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs;

/// <summary>
/// Sends the trial ending reminder, moves organizations whose no-card trial has ended to the free plan, and removes
/// expired mailbox trial claims.
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
    private DateTime? _lastRunUtc;

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
        _lastRunUtc = _timeProvider.GetUtcNow().UtcDateTime;
        context.CancellationToken.ThrowIfCancellationRequested();
        int processed = 0;
        int failed = 0;

        try
        {
            long removed = await _billingManager.RemoveExpiredTrialClaimsAsync();
            if (removed is > 0)
                _logger.LogInformation("Removed {Count} expired trial claims", removed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Expired claims don't block trials, so a failed cleanup only delays removal until the next run.
            _logger.LogError(ex, "Error removing expired trial claims: {Message}", ex.Message);
        }

        var results = await _organizationRepository.FindAsync(q => q
            .FieldEquals(o => o.PlanId, _plans.TrialPlan.Id)
            .OnlyIds(), o => o.SearchAfterPaging().PageLimit(100));

        while (results.Hits.Count is > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            foreach (string organizationId in results.Hits.Where(h => h.Id is not null).Select(h => h.Id!))
            {
                using var scope = _logger.BeginScope(new ExceptionlessState().Organization(organizationId));
                if (context.CancellationToken.IsCancellationRequested)
                    return JobResult.Cancelled;

                try
                {
                    await ProcessOrganizationAsync(organizationId);
                    processed++;
                }
                catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                {
                    // Stop here instead of fetching the next page and renewing the lock; the next run picks up the rest.
                    return JobResult.Cancelled;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, "Error processing trial for organization {OrganizationId}: {Message}", organizationId, ex.Message);
                }
            }

            if (context.CancellationToken.IsCancellationRequested)
                break;

            bool hasNextPage = await results.NextPageAsync();
            if (!hasNextPage)
                break;

            await context.RenewLockAsync();
        }

        _logger.LogInformation("Processed {ProcessedCount} trial organizations with {FailedCount} failures", processed, failed);
        return context.CancellationToken.IsCancellationRequested ? JobResult.Cancelled : JobResult.Success;
    }

    private async Task ProcessOrganizationAsync(string organizationId)
    {
        await using var billingLock = await _billingManager.AcquireOrganizationLockAsync(organizationId);
        // The billing lock serializes writers, but a stale cache entry can still predate a trial extension.
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization is null || !_plans.IsTrialPlan(organization.PlanId))
            return;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (organization.TrialEndsUtc is null || !organization.TrialEndsUtc.Value.IsAfter(utcNow))
        {
            _logger.LogInformation("Trial ended for organization {OrganizationId}, moving to the free plan", organization.Id);
            _billingManager.EndTrial(organization);
            await _billingManager.UpdatePlanLimitSuspensionAsync(organization);
            await _organizationRepository.SaveAsync(organization, o => o.Cache().Originals());
            await _messagePublisher.PublishAsync(new PlanChanged { OrganizationId = organization.Id });
            return;
        }

        if (organization.TrialReminderSentUtc is not null || organization.TrialEndsUtc.Value.AddDays(-_billingOptions.TrialReminderDays).IsAfter(utcNow))
            return;

        var users = await _userRepository.GetByOrganizationIdAsync(organization.Id, o => o.SearchAfterPaging().PageLimit(100));
        do
        {
            foreach (var user in users.Documents.Where(u => u.IsEmailAddressVerified && u.EmailNotificationsEnabled))
                await _mailer.SendOrganizationTrialEndingAsync(user, organization);
        } while (await users.NextPageAsync());

        // Mark the reminder only after every email is queued, so a failure is retried on the next run.
        organization.TrialReminderSentUtc = utcNow;
        await _organizationRepository.SaveAsync(organization, o => o.Cache().Originals());

        _logger.LogInformation("Sent trial ending reminder for organization {OrganizationId}", organization.Id);
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_lastRunUtc.HasValue)
            return Task.FromResult(HealthCheckResult.Healthy("Job has not been run yet."));

        if (_timeProvider.GetUtcNow().UtcDateTime.Subtract(_lastRunUtc.Value) > TimeSpan.FromMinutes(65))
            return Task.FromResult(HealthCheckResult.Unhealthy("Job has not run in the last 65 minutes."));

        return Task.FromResult(HealthCheckResult.Healthy("Job has run in the last 65 minutes."));
    }
}
