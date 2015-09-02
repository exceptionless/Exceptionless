using System;
using System.Collections.Generic;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IOrganizationRepository : IRepository<Organization> {
        Organization GetByInviteToken(string token, out Invite invite);
        Organization GetByStripeCustomerId(string customerId);
        ICollection<Organization> GetAbandoned(int? limit = 20);
        ICollection<Organization> GetByRetentionDaysEnabled(PagingOptions paging);
        ICollection<Organization> GetByCriteria(string criteria, PagingOptions paging, OrganizationSortBy sortBy, bool? paid = null, bool? suspended = null);
        BillingPlanStats GetBillingPlanStats();
        bool IncrementUsage(string organizationId, bool tooBig, int count = 1);
        int GetRemainingEventLimit(string organizationId);
    }
}