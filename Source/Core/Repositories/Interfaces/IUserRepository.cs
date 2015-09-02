using System;
using System.Collections.Generic;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories {
    public interface IUserRepository : IRepository<User> {
        User GetByEmailAddress(string emailAddress);
        User GetByPasswordResetToken(string token);
        User GetUserByOAuthProvider(string provider, string providerUserId);
        User GetByVerifyEmailAddressToken(string token);
        ICollection<User> GetByOrganizationId(string id);
    }
}