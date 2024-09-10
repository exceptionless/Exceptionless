using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Core.Billing;

public class BillingManager
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly BillingPlans _plans;
    private readonly TimeProvider _timeProvider;

    public BillingManager(IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IUserRepository userRepository, BillingPlans plans, TimeProvider timeProvider)
    {
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _userRepository = userRepository;
        _plans = plans;
        _timeProvider = timeProvider;
    }

    public async Task<bool> CanAddOrganizationAsync(User? user)
    {
        if (user is null)
            return false;

        var organizations = (await _organizationRepository.GetByIdsAsync(user.OrganizationIds.ToArray())).Where(o => o.PlanId == _plans.FreePlan.Id);
        return !organizations.Any();
    }

    public async Task<bool> CanAddUserAsync(Organization organization)
    {
        if (String.IsNullOrWhiteSpace(organization?.Id))
            return false;

        long numberOfUsers = (await _userRepository.GetByOrganizationIdAsync(organization.Id)).Total + organization.Invites.Count;
        return organization.MaxUsers <= -1 || numberOfUsers < organization.MaxUsers;
    }

    public async Task<bool> CanAddProjectAsync(Project project)
    {
        if (String.IsNullOrWhiteSpace(project?.OrganizationId))
            return false;

        var organization = await _organizationRepository.GetByIdAsync(project.OrganizationId);
        if (organization is null)
            return false;

        long projectCount = await _projectRepository.GetCountByOrganizationIdAsync(project.OrganizationId);
        return organization.MaxProjects == -1 || projectCount < organization.MaxProjects;
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

        long currentNumberOfUsers = (await _userRepository.GetByOrganizationIdAsync(organization.Id)).Total + organization.Invites.Count;
        int maxUsers = plan.MaxUsers != -1 ? plan.MaxUsers : Int32.MaxValue;
        if (currentNumberOfUsers > maxUsers)
            return ChangePlanResult.FailWithMessage($"Please remove {currentNumberOfUsers - maxUsers} user{((currentNumberOfUsers - maxUsers) > 0 ? "s" : String.Empty)} and try again.");

        int maxProjects = plan.MaxProjects != -1 ? plan.MaxProjects : Int32.MaxValue;
        long projectCount = await _projectRepository.GetCountByOrganizationIdAsync(organization.Id);
        if (projectCount > maxProjects)
            return ChangePlanResult.FailWithMessage($"Please remove {projectCount - maxProjects} project{((projectCount - maxProjects) > 0 ? "s" : String.Empty)} and try again.");

        // Ensure the user can't be apart of more than one free plan.
        if (String.Equals(plan.Id, _plans.FreePlan.Id) && user is not null && (await _organizationRepository.GetByIdsAsync(user.OrganizationIds.ToArray())).Any(o => String.Equals(o.PlanId, _plans.FreePlan.Id)))
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
}
