using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using ElasticInfer = Elastic.Clients.Elasticsearch.Infer;

namespace Exceptionless.Core.Repositories;

public class OrganizationRepository : RepositoryBase<Organization>, IOrganizationRepository
{
    private readonly BillingPlans _plans;
    private readonly TimeProvider _timeProvider;

    public OrganizationRepository(ExceptionlessElasticConfiguration configuration, IValidator<Organization> validator, BillingPlans plans, AppOptions options)
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

    public Task<FindResults<Organization>> GetByCriteriaAsync(string? criteria, CommandOptionsDescriptor<Organization> options, OrganizationSortBy sortBy, bool? paid = null, bool? suspended = null)
    {
        var query = new RepositoryQuery<Organization>();

        if (!String.IsNullOrWhiteSpace(criteria))
            query.ElasticFilter(new BoolQuery
            {
                Should = [
                    new TermQuery { Field = ElasticInfer.Field<Organization>(o => o.Id), Value = criteria },
                    new TermQuery { Field = ElasticInfer.Field<Organization>(o => o.Name), Value = criteria }
                ],
                MinimumShouldMatch = 1
            });

        if (paid.HasValue)
        {
            if (paid.Value)
                query.ElasticFilter(new BoolQuery { MustNot = [new TermQuery { Field = ElasticInfer.Field<Organization>(o => o.PlanId), Value = _plans.FreePlan.Id }] });
            else
                query.FieldEquals(o => o.PlanId, _plans.FreePlan.Id);
        }

        if (suspended.HasValue)
        {
            if (suspended.Value)
                query.ElasticFilter(new BoolQuery
                {
                    Should = [
                        new BoolQuery { MustNot = [
                            new TermQuery { Field = ElasticInfer.Field<Organization>(o => o.BillingStatus), Value = (int)BillingStatus.Active },
                            new TermQuery { Field = ElasticInfer.Field<Organization>(o => o.BillingStatus), Value = (int)BillingStatus.Trialing },
                            new TermQuery { Field = ElasticInfer.Field<Organization>(o => o.BillingStatus), Value = (int)BillingStatus.Canceled }
                        ] },
                        new TermQuery { Field = ElasticInfer.Field<Organization>(o => o.IsSuspended), Value = true }
                    ],
                    MinimumShouldMatch = 1
                });
            else
                query.ElasticFilter(new BoolQuery
                {
                    Should = [
                        new BoolQuery { Should = [
                            new TermQuery { Field = ElasticInfer.Field<Organization>(o => o.BillingStatus), Value = (int)BillingStatus.Active },
                            new TermQuery { Field = ElasticInfer.Field<Organization>(o => o.BillingStatus), Value = (int)BillingStatus.Trialing },
                            new TermQuery { Field = ElasticInfer.Field<Organization>(o => o.BillingStatus), Value = (int)BillingStatus.Canceled }
                        ], MinimumShouldMatch = 1 },
                        new TermQuery { Field = ElasticInfer.Field<Organization>(o => o.IsSuspended), Value = false }
                    ],
                    MinimumShouldMatch = 1
                });
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
                query.SortAscending((Field)"name.keyword");
                break;
        }

        return FindAsync(q => query, options);
    }

    public async Task<BillingPlanStats> GetBillingPlanStatsAsync()
    {
        var results = (await FindAsync(q => q
            .Include(o => o.PlanId, o => o.IsSuspended, o => o.BillingPrice, o => o.BillingStatus)
            .SortDescending(o => o.PlanId))).Documents;
        var smallOrganizations = results.Where(o => String.Equals(o.PlanId, _plans.SmallPlan.Id) && o.BillingPrice > 0).ToList();
        var mediumOrganizations = results.Where(o => String.Equals(o.PlanId, _plans.MediumPlan.Id) && o.BillingPrice > 0).ToList();
        var largeOrganizations = results.Where(o => String.Equals(o.PlanId, _plans.LargePlan.Id) && o.BillingPrice > 0).ToList();
        decimal monthlyTotalPaid = smallOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice)
            + mediumOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice)
            + largeOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice);

        var smallYearlyOrganizations = results.Where(o => String.Equals(o.PlanId, _plans.SmallYearlyPlan.Id) && o.BillingPrice > 0).ToList();
        var mediumYearlyOrganizations = results.Where(o => String.Equals(o.PlanId, _plans.MediumYearlyPlan.Id) && o.BillingPrice > 0).ToList();
        var largeYearlyOrganizations = results.Where(o => String.Equals(o.PlanId, _plans.LargeYearlyPlan.Id) && o.BillingPrice > 0).ToList();
        decimal yearlyTotalPaid = smallYearlyOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice)
            + mediumYearlyOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice)
            + largeYearlyOrganizations.Where(o => !o.IsSuspended && o.BillingStatus == BillingStatus.Active).Sum(o => o.BillingPrice);

        return new BillingPlanStats
        {
            SmallTotal = smallOrganizations.Count,
            SmallYearlyTotal = smallYearlyOrganizations.Count,
            MediumTotal = mediumOrganizations.Count,
            MediumYearlyTotal = mediumYearlyOrganizations.Count,
            LargeTotal = largeOrganizations.Count,
            LargeYearlyTotal = largeYearlyOrganizations.Count,
            MonthlyTotal = monthlyTotalPaid + (yearlyTotalPaid / 12),
            YearlyTotal = (monthlyTotalPaid * 12) + yearlyTotalPaid,
            MonthlyTotalAccounts = smallOrganizations.Count + mediumOrganizations.Count + largeOrganizations.Count,
            YearlyTotalAccounts = smallYearlyOrganizations.Count + mediumYearlyOrganizations.Count + largeYearlyOrganizations.Count,
            FreeAccounts = results.Count(o => String.Equals(o.PlanId, _plans.FreePlan.Id)),
            PaidAccounts = results.Count(o => !String.Equals(o.PlanId, _plans.FreePlan.Id) && o.BillingPrice > 0),
            FreeloaderAccounts = results.Count(o => !String.Equals(o.PlanId, _plans.FreePlan.Id) && o.BillingPrice <= 0),
            SuspendedAccounts = results.Count(o => o.IsSuspended),
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
