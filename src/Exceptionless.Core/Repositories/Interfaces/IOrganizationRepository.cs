using System;
using System.Threading.Tasks;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public interface IOrganizationRepository : ISearchableRepository<Organization> {
        Task<Organization> GetByInviteTokenAsync(string token);
        Task<Organization> GetByStripeCustomerIdAsync(string customerId);
        Task<FindResults<Organization>> GetByRetentionDaysEnabledAsync(CommandOptionsDescriptor<Organization> options = null);
        Task<FindResults<Organization>> GetByCriteriaAsync(string criteria, CommandOptionsDescriptor<Organization> options, OrganizationSortBy sortBy, bool? paid = null, bool? suspended = null);
        Task<BillingPlanStats> GetBillingPlanStatsAsync();
    }
}
