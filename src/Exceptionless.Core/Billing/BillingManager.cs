using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Foundatio.Lock;

namespace Exceptionless.Core.Billing;

public class BillingManager
{
    private static readonly TimeSpan BillingLockDuration = TimeSpan.FromMinutes(5);
    private readonly ILockProvider _lockProvider;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly BillingPlans _plans;
    private readonly BillingOptions _billingOptions;
    private readonly TimeProvider _timeProvider;

    public BillingManager(ILockProvider lockProvider, IOrganizationRepository organizationRepository, BillingPlans plans, AppOptions appOptions, TimeProvider timeProvider)
    {
        _lockProvider = lockProvider;
        _organizationRepository = organizationRepository;
        _plans = plans;
        _billingOptions = appOptions.BillingOptions;
        _timeProvider = timeProvider;
    }

    public Task<ILock> AcquireOrganizationLockAsync(string organizationId)
        => _lockProvider.AcquireAsync(GetOrganizationLockKey(organizationId), BillingLockDuration, TimeSpan.FromSeconds(30));

    public Task<ILock?> TryAcquireOrganizationLockAsync(string organizationId)
        => _lockProvider.TryAcquireAsync(GetOrganizationLockKey(organizationId), BillingLockDuration, TimeSpan.Zero);

    public async Task<bool> CanAddOrganizationAsync(User? user)
    {
        if (user is null)
            return false;

        var organizations = await _organizationRepository.GetByIdsAsync(user.OrganizationIds.ToArray());
        return !organizations.Any(o => _plans.IsFreeOrTrialPlan(o.PlanId));
    }

    /// <summary>
    /// A user gets one no-card trial, and only for the first organization they create.
    /// </summary>
    public bool CanStartTrial(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return _billingOptions.TrialDays > 0 && user.TrialStartedUtc is null && user.OrganizationIds.Count == 0;
    }

    public void StartTrial(Organization organization, User user)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ArgumentNullException.ThrowIfNull(user);

        ApplyTrialPlan(organization, user);
        user.TrialStartedUtc = _timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Applies the trial plan and starts a full-length trial. Also used by admins to extend a trial.
    /// </summary>
    public void ApplyTrialPlan(Organization organization, User? user = null)
    {
        ArgumentNullException.ThrowIfNull(organization);

        int trialDays = _billingOptions.TrialDays > 0 ? _billingOptions.TrialDays : 7;
        ApplyBillingPlan(organization, _plans.TrialPlan, user);
        organization.TrialEndsUtc = _timeProvider.GetUtcNow().UtcDateTime.AddDays(trialDays);
        organization.TrialReminderSentUtc = null;
    }

    public void EndTrial(Organization organization)
    {
        ArgumentNullException.ThrowIfNull(organization);
        ApplyBillingPlan(organization, _plans.FreePlan);
    }

    public async Task<bool> HasPremiumFeaturesAsync(string organizationId)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        if (organization is null)
            return false;

        return organization.HasPremiumFeatures;
    }

    public async Task<ChangePlanResult> CanDownGradeAsync(Organization organization, BillingPlan plan, User? user)
    {
        if (String.IsNullOrWhiteSpace(organization?.Id))
            return ChangePlanResult.FailWithMessage("Invalid Organization");

        // Ensure the user can't be apart of more than one free or trial plan.
        if (_plans.IsFreePlan(plan.Id) && user is not null && (await _organizationRepository.GetByIdsAsync(user.OrganizationIds.ToArray())).Any(o => !String.Equals(o.Id, organization.Id) && _plans.IsFreeOrTrialPlan(o.PlanId)))
            return ChangePlanResult.FailWithMessage("You already have one free account. You are not allowed to create more than one free account.");

        return new ChangePlanResult { Success = true };
    }

    public BillingPlan? GetBillingPlan(string planId)
    {
        return _plans.Plans.FirstOrDefault(p => String.Equals(p.Id, planId, StringComparison.OrdinalIgnoreCase));
    }

    public BillingPlan? GetBillingPlanByUpsellingRetentionPeriod(int retentionDays)
    {
        return _plans.Plans.Where(p => p.RetentionDays > retentionDays && p.Price > 0).OrderBy(p => p.RetentionDays).ThenBy(p => p.Price).FirstOrDefault();
    }

    public void ApplyBillingPlan(Organization organization, BillingPlan plan, User? user = null, bool updateBillingPrice = true)
    {
        organization.PlanId = plan.Id;
        organization.PlanName = plan.Name;
        organization.PlanDescription = plan.Description;
        organization.BillingChangeDate = _timeProvider.GetUtcNow().UtcDateTime;

        if (updateBillingPrice)
            organization.BillingPrice = plan.Price;

        if (user is not null)
            organization.BillingChangedByUserId = user.Id;

        organization.MaxUsers = plan.MaxUsers;
        organization.MaxProjects = plan.MaxProjects;
        organization.RetentionDays = plan.RetentionDays;
        organization.MaxEventsPerMonth = plan.MaxEventsPerMonth;
        organization.HasPremiumFeatures = plan.HasPremiumFeatures;

        organization.GetCurrentUsage(_timeProvider).Limit = organization.GetMaxEventsPerMonthWithBonus(_timeProvider);
    }

    public void ApplyBonus(Organization organization, int bonusEvents, DateTime? expires = null)
    {
        organization.BonusEventsPerMonth = bonusEvents;
        organization.BonusExpiration = expires;
        organization.GetCurrentUsage(_timeProvider).Limit = organization.GetMaxEventsPerMonthWithBonus(_timeProvider);
    }

    internal void SetStripeSubscriptionId(Organization organization, string? subscriptionId)
    {
        ArgumentNullException.ThrowIfNull(organization);
        if (String.Equals(organization.StripeSubscriptionId, subscriptionId, StringComparison.Ordinal))
            return;

        organization.StripeSubscriptionId = subscriptionId;
        organization.StripeSubscriptionEventDate = null;
    }

    private static string GetOrganizationLockKey(string organizationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        return $"Organization:{organizationId}:billing-plan";
    }
}
