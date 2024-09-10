using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Foundatio.Repositories.Utility;

namespace Exceptionless.Tests.Utility;

public class OrganizationData
{
    private readonly TimeProvider _timeProvider;

    public OrganizationData(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }
    public IEnumerable<Organization> GenerateOrganizations(BillingManager billingManager, BillingPlans plans, int count = 10, bool generateId = false, string? id = null)
    {
        for (int i = 0; i < count; i++)
            yield return GenerateOrganization(billingManager, plans, generateId, id);
    }

    public List<Organization> GenerateSampleOrganizations(BillingManager billingManager, BillingPlans plans)
    {
        return
        [
            GenerateSampleOrganization(billingManager, plans),
            GenerateOrganization(billingManager, plans, id: TestConstants.OrganizationId2,
                inviteEmail: TestConstants.InvitedOrganizationUserEmail),
            GenerateOrganization(billingManager, plans, id: TestConstants.OrganizationId3,
                inviteEmail: TestConstants.InvitedOrganizationUserEmail),
            GenerateOrganization(billingManager, plans, id: TestConstants.OrganizationId4,
                inviteEmail: TestConstants.InvitedOrganizationUserEmail),
            GenerateOrganization(billingManager, plans, id: TestConstants.SuspendedOrganizationId,
                inviteEmail: TestConstants.InvitedOrganizationUserEmail, isSuspended: true)
        ];
    }

    public Organization GenerateSampleOrganization(BillingManager billingManager, BillingPlans plans)
    {
        return GenerateOrganization(billingManager, plans, id: TestConstants.OrganizationId, name: "Acme", inviteEmail: TestConstants.InvitedOrganizationUserEmail);
    }

    public Organization GenerateSampleOrganizationWithPlan(BillingManager billingManager, BillingPlans plans, BillingPlan plan)
    {
        return GenerateOrganization(billingManager, plans, id: TestConstants.OrganizationId, name: "Acme", inviteEmail: TestConstants.InvitedOrganizationUserEmail, plan: plan);
    }

    public Organization GenerateOrganization(BillingManager billingManager, BillingPlans plans, bool generateId = false, string? name = null, string? id = null, string? inviteEmail = null, bool isSuspended = false, BillingPlan? plan = null)
    {
        var organization = new Organization
        {
            Id = id.IsNullOrEmpty() ? generateId ? ObjectId.GenerateNewId().ToString() : TestConstants.OrganizationId : id,
            Name = name ?? $"Organization{id}"
        };

        billingManager.ApplyBillingPlan(organization, plan ?? plans.UnlimitedPlan);
        if (organization.BillingPrice > 0)
        {
            organization.StripeCustomerId = "stripe_customer_id";
            organization.CardLast4 = "1234";
            organization.SubscribeDate = _timeProvider.GetUtcNow().UtcDateTime;
            organization.BillingChangeDate = _timeProvider.GetUtcNow().UtcDateTime;
            organization.BillingChangedByUserId = TestConstants.UserId;
        }

        if (!String.IsNullOrEmpty(inviteEmail))
        {
            organization.Invites.Add(new Invite
            {
                EmailAddress = inviteEmail,
                Token = Guid.NewGuid().ToString(),
                DateAdded = _timeProvider.GetUtcNow().UtcDateTime
            });
        }

        if (isSuspended)
        {
            organization.IsSuspended = true;
            organization.SuspensionCode = SuspensionCode.Abuse;
            organization.SuspendedByUserId = TestConstants.UserId;
            organization.SuspensionDate = _timeProvider.GetUtcNow().UtcDateTime;
        }

        return organization;
    }
}
