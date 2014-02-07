#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using MongoDB.Driver;

namespace Exceptionless.Core {
    /// <summary>
    /// IMongoRepositoryManager definition.
    /// </summary>
    /// <typeparam name="T">The type contained in the repository.</typeparam>
    public interface IMongoRepositoryManager<T> {
        /// <summary>
        /// Initializes a collection.
        /// </summary>
        /// <param name="collection">The collection.</param>
        void InitializeCollection(MongoCollection<T> collection);
    }
}