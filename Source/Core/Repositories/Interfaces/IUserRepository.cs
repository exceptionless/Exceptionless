using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IUserRepository : IRepository<User> {
        User GetByEmailAddress(string emailAddress);
        User GetByPasswordResetToken(string token);
        User GetUserByOAuthProvider(string provider, string providerUserId);
        User GetByVerifyEmailAddressToken(string token);
        long CountByOrganizationId(string organizationId);
        FindResults<User> GetByOrganizationId(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
        FindResults<User> GetByOrganizationIds(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null);
    }
}