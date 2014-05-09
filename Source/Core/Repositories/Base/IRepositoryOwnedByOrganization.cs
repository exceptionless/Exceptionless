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
using System.Threading.Tasks;
using Exceptionless.Models;

namespace Exceptionless.Core.Repositories {
    public interface IRepositoryOwnedByOrganization<T> : IRepository<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        IList<T> GetByOrganizationId(string organizationId, bool useCache = false, TimeSpan? expiresIn = null);
        IList<T> GetByOrganizationId(IList<string> organizationIds, bool useCache = false, TimeSpan? expiresIn = null);
        void RemoveAllByOrganizationId(string organizationId);
        Task RemoveAllByOrganizationIdAsync(string organizationId);
    }
}