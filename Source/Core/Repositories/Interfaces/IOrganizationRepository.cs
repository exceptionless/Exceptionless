﻿using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Models;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IOrganizationRepository : IRepository<Organization>, IElasticReadOnlyRepository<Organization> {
        Task<Organization> GetByInviteTokenAsync(string token);
        Task<Organization> GetByStripeCustomerIdAsync(string customerId);
        Task<FindResults<Organization>> GetByRetentionDaysEnabledAsync(PagingOptions paging);
        Task<FindResults<Organization>> GetByCriteriaAsync(string criteria, PagingOptions paging, OrganizationSortBy sortBy, bool? paid = null, bool? suspended = null);
        Task<BillingPlanStats> GetBillingPlanStatsAsync();
        Task<bool> IncrementUsageAsync(string organizationId, bool tooBig, int count = 1, bool applyHourlyLimit = true);
        Task<int> GetRemainingEventLimitAsync(string organizationId);
    }
}
