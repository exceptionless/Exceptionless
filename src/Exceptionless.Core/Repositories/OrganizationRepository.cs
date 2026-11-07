using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories;

public class OrganizationRepository : RepositoryBase<Organization>, IOrganizationRepository
{
    private readonly BillingPlans _plans;
    private readonly TimeProvider _timeProvider;

    public OrganizationRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, BillingPlans plans, AppOptions options)
        : base(configuration.Organizations, validator, options)
    {
        _plans = plans;
        _timeProvider = configuration.TimeProvider;
        DocumentsChanging.AddSyncHandler(OnDocumentsChanging);
    }

    private void OnDocumentsChanging(object sender, DocumentsChangeEventArgs<Organization> args)
    {
        foreach (var organization in args.Documents)
            organization.Value.TrimUsage(_timeProvider);
    }

    public async Task<Organization?> GetByInviteTokenAsync(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var hit = await FindOneAsync(q => q.FieldEquals(o => o.Invites.First().Token, token));
        return hit?.Document;
    }

    public async Task<Organization?> GetByStripeCustomerIdAsync(string customerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(customerId);

        var hit = await FindOneAsync(q => q.FieldEquals(o => o.StripeCustomerId, customerId));
        return hit?.Document;
    }

    public Task<FindResults<Organization>> GetByFilterAsync(AppFilter systemFilter, string? userFilter, string? sort, CommandOptionsDescriptor<Organization>? options = null)
    {
        IRepositoryQuery<Organization> query = new RepositoryQuery<Organization>()
            .AppFilter(systemFilter)
            .FilterExpression(userFilter);

        query = !String.IsNullOrEmpty(sort) ? query.SortExpression(sort) : query.SortAscending(o => o.Name);
        return FindAsync(q => query, options);
    }

    public Task<FindResults<Organization>> GetByCriteriaAsync(string? criteria, CommandOptionsDescriptor<Organization> options, OrganizationSortBy sortBy, bool? paid = null, bool? suspended = null)
    {
        var query = new RepositoryQuery<Organization>();

        if (!String.IsNullOrWhiteSpace(criteria))
        {
            string normalizedCriteria = criteria.Trim();
            query.FieldOr(g => g
                .FieldEquals(o => o.Id, normalizedCriteria)
                .FieldEquals(o => o.Name, normalizedCriteria)
                .FieldContains(o => o.Name, normalizedCriteria));
        }

        if (paid.HasValue)
        {
            if (paid.Value)
                query.FieldNotEquals(o => o.PlanId, _plans.FreePlan.Id, _plans.TrialPlan.Id);
            else
                query.FieldEquals(o => o.PlanId, _plans.FreePlan.Id, _plans.TrialPlan.Id);
        }

        if (suspended.HasValue)
        {
            if (suspended.Value)
                query.FieldOr(g => g
                    .FieldNot(n => n
                        .FieldOr(o => o
                            .FieldEquals(o => o.BillingStatus, (int)BillingStatus.Active)
                            .FieldEquals(o => o.BillingStatus, (int)BillingStatus.Trialing)
                            .FieldEquals(o => o.BillingStatus, (int)BillingStatus.Canceled)))
                    .FieldEquals(o => o.IsSuspended, true));
            else
                query.FieldAnd(g => g
                    .FieldOr(o => o
                        .FieldEquals(o => o.BillingStatus, (int)BillingStatus.Active)
                        .FieldEquals(o => o.BillingStatus, (int)BillingStatus.Trialing)
                        .FieldEquals(o => o.BillingStatus, (int)BillingStatus.Canceled))
                    .FieldEquals(o => o.IsSuspended, false));
        }

        switch (sortBy)
        {
            case OrganizationSortBy.Newest:
                query.SortDescending((Organization o) => o.Id);
                break;
            case OrganizationSortBy.Subscribed:
                query.SortDescending((Organization o) => o.SubscribeDate);
                break;
            // case OrganizationSortBy.MostActive:
            //    query.WithSortDescending((Organization o) => o.TotalEventCount);
            //    break;
            default:
                query.SortAscending((Organization o) => o.Name);
                break;
        }

        return FindAsync(q => query, options);
    }

    internal const int BillingPlanStatsPageLimit = 250;

    public async Task<BillingPlanStats> GetBillingPlanStatsAsync()
    {
        var page = await FindAsync(q => q
            .Include(o => o.PlanId, o => o.IsSuspended, o => o.BillingPrice, o => o.BillingStatus, o => o.TrialEndsUtc)
            .SortAscending(o => o.Id), o => o.SearchAfterPaging().PageLimit(BillingPlanStatsPageLimit));
        int smallTotal = 0, mediumTotal = 0, largeTotal = 0, smallYearlyTotal = 0, mediumYearlyTotal = 0, largeYearlyTotal = 0;
        int freeAccounts = 0, trialAccounts = 0, convertedTrialAccounts = 0, expiredTrialAccounts = 0, paidAccounts = 0, freeloaderAccounts = 0, suspendedAccounts = 0;
        decimal monthlyTotalPaid = 0, yearlyTotalPaid = 0;

        // Count page by page so memory stays bounded by the page size, not the number of organizations.
        do
        {
            foreach (var organization in page.Documents)
            {
                bool isFree = _plans.IsFreePlan(organization.PlanId);
                bool isFreeOrTrial = _plans.IsFreeOrTrialPlan(organization.PlanId);
                bool isPaying = organization.BillingPrice > 0;

                if (isFree)
                    freeAccounts++;
                if (_plans.IsTrialPlan(organization.PlanId))
                    trialAccounts++;
                if (organization.TrialEndsUtc is not null && !isFreeOrTrial && isPaying)
                    convertedTrialAccounts++;
                if (organization.TrialEndsUtc is not null && isFree)
                    expiredTrialAccounts++;
                if (!isFreeOrTrial && isPaying)
                    paidAccounts++;
                if (!isFreeOrTrial && !isPaying)
                    freeloaderAccounts++;
                if (organization.IsSuspended)
                    suspendedAccounts++;

                if (!isPaying)
                    continue;

                decimal activePrice = !organization.IsSuspended && organization.BillingStatus is BillingStatus.Active ? organization.BillingPrice : 0;
                if (String.Equals(organization.PlanId, _plans.SmallPlan.Id, StringComparison.Ordinal))
                {
                    smallTotal++;
                    monthlyTotalPaid += activePrice;
                }
                else if (String.Equals(organization.PlanId, _plans.MediumPlan.Id, StringComparison.Ordinal))
                {
                    mediumTotal++;
                    monthlyTotalPaid += activePrice;
                }
                else if (String.Equals(organization.PlanId, _plans.LargePlan.Id, StringComparison.Ordinal))
                {
                    largeTotal++;
                    monthlyTotalPaid += activePrice;
                }
                else if (String.Equals(organization.PlanId, _plans.SmallYearlyPlan.Id, StringComparison.Ordinal))
                {
                    smallYearlyTotal++;
                    yearlyTotalPaid += activePrice;
                }
                else if (String.Equals(organization.PlanId, _plans.MediumYearlyPlan.Id, StringComparison.Ordinal))
                {
                    mediumYearlyTotal++;
                    yearlyTotalPaid += activePrice;
                }
                else if (String.Equals(organization.PlanId, _plans.LargeYearlyPlan.Id, StringComparison.Ordinal))
                {
                    largeYearlyTotal++;
                    yearlyTotalPaid += activePrice;
                }
            }
        } while (await page.NextPageAsync());

        return new BillingPlanStats
        {
            SmallTotal = smallTotal,
            SmallYearlyTotal = smallYearlyTotal,
            MediumTotal = mediumTotal,
            MediumYearlyTotal = mediumYearlyTotal,
            LargeTotal = largeTotal,
            LargeYearlyTotal = largeYearlyTotal,
            MonthlyTotal = monthlyTotalPaid + (yearlyTotalPaid / 12),
            YearlyTotal = (monthlyTotalPaid * 12) + yearlyTotalPaid,
            MonthlyTotalAccounts = smallTotal + mediumTotal + largeTotal,
            YearlyTotalAccounts = smallYearlyTotal + mediumYearlyTotal + largeYearlyTotal,
            FreeAccounts = freeAccounts,
            TrialAccounts = trialAccounts,
            ConvertedTrialAccounts = convertedTrialAccounts,
            ExpiredTrialAccounts = expiredTrialAccounts,
            PaidAccounts = paidAccounts,
            FreeloaderAccounts = freeloaderAccounts,
            SuspendedAccounts = suspendedAccounts,
        };
    }
}

public enum OrganizationSortBy
{
    Newest = 0,
    Subscribed = 1,
    MostActive = 2,
    Alphabetical = 3,
}
