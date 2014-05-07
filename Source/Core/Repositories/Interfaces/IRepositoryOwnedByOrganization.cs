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
using System.Linq;
using Exceptionless.Models;

namespace Exceptionless.Core {
    public interface IRepositoryOwnedByOrganization<T> : IRepositoryWithIdentity<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        /// <summary>
        /// Returns all entities that are within the specified organization.
        /// </summary>
        /// <param name="id">The organization id.</param>
        /// <returns>A list of entities belonging to the organization.</returns>
        IEnumerable<T> GetByOrganizationId(string id);

        /// <summary>
        /// Returns all entities that are within the specified list of organizations.
        /// </summary>
        /// <param name="ids">The list of organization ids.</param>
        /// <returns>A list of entities belonging to the specified organizations.</returns>
        IEnumerable<T> GetByOrganizationId(IEnumerable<string> ids);

        /// <summary>
        /// Returns a queryable list of entities that are within the specified organization.
        /// </summary>
        /// <param name="id">The organization id.</param>
        /// <returns>A list of entities belonging to the organization.</returns>
        IQueryable<T> WhereForOrganization(string id);

        /// <summary>
        /// Returns a queryable list of entities that are within the specified list of organizations.
        /// </summary>
        /// <param name="ids">The list of organization ids.</param>
        /// <returns>A list of entities belonging to the specified organizations.</returns>
        IQueryable<T> WhereForOrganization(IEnumerable<string> ids);
    }
}