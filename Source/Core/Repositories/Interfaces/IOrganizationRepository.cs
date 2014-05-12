#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using Exceptionless.Models;

namespace Exceptionless.Core.Repositories {
    public interface IOrganizationRepository : IRepository<Organization> {
        Organization GetByInviteToken(string token, out Invite invite);
        Organization GetByStripeCustomerId(string customerId);
        IList<Organization> GetStaleAccounts(int? limit = 20);
        IList<Organization> GetWithRetentionDaysGreaterThanZero(PagingOptions paging);
        void SetStats(string organizationId, long? projectCount = null, long? errorCount = null, long? stackCount = null);

    }
}