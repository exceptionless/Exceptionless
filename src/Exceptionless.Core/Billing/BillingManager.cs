using Exceptionless.Core.Configuration;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Foundatio.Lock;
using Foundatio.Repositories;

namespace Exceptionless.Core.Billing;

public class BillingManager
{
    private static readonly TimeSpan BillingLockDuration = TimeSpan.FromMinutes(5);
    private readonly ILockProvider _lockProvider;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly BillingPlans _plans;
    private readonly BillingOptions _billingOptions;
    private readonly TimeProvider _timeProvider;

    public BillingManager(ILockProvider lockProvider, IOrganizationRepository organizationRepository, IUserRepository userRepository, BillingPlans plans, AppOptions appOptions, TimeProvider timeProvider)
    {
        _lockProvider = lockProvider;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _plans = plans;
        _billingOptions = appOptions.BillingOptions;
        _timeProvider = timeProvider;
    }

    public Task<ILock> AcquireOrganizationLockAsync(string organizationId)
        => _lockProvider.AcquireAsync(GetOrganizationLockKey(organizationId), BillingLockDuration, TimeSpan.FromSeconds(30));

    public Task<ILock> AcquireUserOrganizationCreationLockAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return _lockProvider.AcquireAsync($"User:{userId}:create-organization", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));
    }

    public Task<ILock?> TryAcquireOrganizationLockAsync(string organizationId)
        => _lockProvider.TryAcquireAsync(GetOrganizationLockKey(organizationId), BillingLockDuration, TimeSpan.Zero);

    public async Task<bool> CanAddOrganizationAsync(User? user)
    {
        if (user is null)
            return false;

        return (await GetOwnedFreeOrTrialOrganizationsAsync(user.Id, user.OrganizationIds)).Count == 0;
    }

    /// <summary>
    /// A user gets one no-card trial, and only for the first organization they own. Being a member of
    /// someone else's organization doesn't count.
    /// </summary>
    public async Task<bool> CanStartTrialAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (_billingOptions.TrialDays <= 0 || user.TrialStartedUtc is not null)
            return false;

        return !await OwnsAnyOrganizationAsync(user.Id, user.OrganizationIds);
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

        // Ensure the organization's owner can't own more than one free or trial organization, no matter which member downgrades it.
        if (_plans.IsFreePlan(plan.Id) && user is not null && await OwnerHasOtherFreeOrTrialOrganizationAsync(organization, user))
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

    private async Task<bool> OwnerHasOtherFreeOrTrialOrganizationAsync(Organization organization, User actingUser)
    {
        string ownerId = organization.CreatedByUserId ?? organization.BillingChangedByUserId ?? actingUser.Id;
        var ownerOrganizationIds = String.Equals(ownerId, actingUser.Id, StringComparison.Ordinal)
            ? actingUser.OrganizationIds
            : (await _userRepository.GetByIdAsync(ownerId))?.OrganizationIds ?? new HashSet<string>();

        var ownedOrganizations = await GetOwnedFreeOrTrialOrganizationsAsync(ownerId, ownerOrganizationIds);
        return ownedOrganizations.Any(o => !String.Equals(o.Id, organization.Id, StringComparison.Ordinal));
    }

    // Ownership is the organization's creator, including organizations the creator is no longer a member of.
    // Organizations created before CreatedByUserId existed fall back to the member who last changed the billing plan.

    private async Task<bool> OwnsAnyOrganizationAsync(string userId, ICollection<string> membershipOrganizationIds)
    {
        var created = await _organizationRepository.CountAsync(q => q.FieldEquals(o => o.CreatedByUserId, userId));
        if (created.Total > 0)
            return true;

        return (await GetLegacyOwnedOrganizationsAsync(userId, membershipOrganizationIds)).Count > 0;
    }

    /// <summary>
    /// The user's free and trial organizations. The one-free-organization rule keeps this to a handful, so a single
    /// page is enough regardless of how many paid organizations the user owns.
    /// </summary>
    private async Task<IReadOnlyCollection<Organization>> GetOwnedFreeOrTrialOrganizationsAsync(string userId, ICollection<string> membershipOrganizationIds)
    {
        var created = await _organizationRepository.FindAsync(q => q
            .FieldEquals(o => o.CreatedByUserId, userId)
            .FieldEquals(o => o.PlanId, _plans.FreePlan.Id, _plans.TrialPlan.Id), o => o.PageLimit(100));
        var legacy = await GetLegacyOwnedOrganizationsAsync(userId, membershipOrganizationIds);

        return created.Documents
            .Concat(legacy.Where(o => _plans.IsFreeOrTrialPlan(o.PlanId)))
            .DistinctBy(o => o.Id)
            .ToList();
    }

    private async Task<IReadOnlyCollection<Organization>> GetLegacyOwnedOrganizationsAsync(string userId, ICollection<string> membershipOrganizationIds)
    {
        if (membershipOrganizationIds.Count == 0)
            return [];

        var memberOrganizations = await _organizationRepository.GetByIdsAsync(membershipOrganizationIds.ToArray());
        return memberOrganizations.Where(o => o.CreatedByUserId is null && String.Equals(o.BillingChangedByUserId, userId, StringComparison.Ordinal)).ToList();
    }

    private static string GetOrganizationLockKey(string organizationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(organizationId);
        return $"Organization:{organizationId}:billing-plan";
    }
}
