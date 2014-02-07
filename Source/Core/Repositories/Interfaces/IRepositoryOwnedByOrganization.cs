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
    public interface IRepositoryOwnedByOrganization<T> : IRepositoryWithIdentity<T> where T : class, IOwnedByOrganization, new() {
        /// <summary>
        /// Returns the T by its given id.
        /// </summary>
        /// <param name="id">The string representing the ObjectId of the entity to retrieve.</param>
        /// <returns>The Entity T.</returns>
        IQueryable<T> GetByOrganizationId(string id);

        /// <summary>
        /// Returns the IQueryable<see cref="T" />> by its given ids.
        /// </summary>
        /// <param name="ids">The string representing the ObjectId of the entities to retrieve.</param>
        /// <returns>An IQueryable<see cref="T" />> with entities that contain one of the passed in ObjectId's.</returns>
        IQueryable<T> GetByOrganizationIds(IEnumerable<string> ids);
    }
}