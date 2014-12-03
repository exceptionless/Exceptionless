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
    public interface IUserRepository : IRepository<User> {
        User GetByEmailAddress(string emailAddress);
        User GetByPasswordResetToken(string token);
        User GetUserByOAuthProvider(string provider, string providerUserId);
        User GetByVerifyEmailAddressToken(string token);
        ICollection<User> GetByOrganizationId(string id);
    }
}