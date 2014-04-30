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
using Exceptionless.Core.Caching;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core {
    /// <summary>
    /// Deals with entities in MongoDb.
    /// </summary>
    /// <typeparam name="T">The type contained in the repository.</typeparam>
    public abstract class MongoRepositoryWithIdentity<T> : MongoRepository<T>, IRepositoryWithIdentity<T> where T : class, IIdentity, new() {
        protected MongoRepositoryWithIdentity(MongoDatabase database, ICacheClient cacheClient = null) : base(database, cacheClient) {}

        public static class FieldNames {
            public const string Id = "_id";
        }

        /// <summary>
        /// Returns the T by its given id.
        /// </summary>
        /// <param name="id">The string representing the ObjectId of the entity to retrieve.</param>
        /// <param name="usePrimary">Force the document to be read from the primary.</param>
        /// <returns>The Entity T.</returns>
        public T GetById(string id, bool usePrimary = false) {
            if (String.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("id");

            T result = null;

            try {
                var findArgs = new FindOneArgs { Query = Query.EQ("_id", new ObjectId(id)) };
                if (usePrimary)
                    findArgs.ReadPreference = ReadPreference.PrimaryPreferred;

                result = _collection.FindOneAs<T>(findArgs);
            } catch (ArgumentOutOfRangeException) {}

            return result;
        }

        /// <summary>
        /// Returns the T by its given id using a cache.
        /// </summary>
        /// <param name="id">The string representing the ObjectId of the entity to retrieve.</param>
        /// <param name="usePrimary">Force the document to be read from the primary.</param>
        /// <returns>The Entity T.</returns>
        public T GetByIdCached(string id, bool usePrimary = false) {
            if (String.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("id");

            var result = Cache.Get<T>(GetScopedCacheKey(id));
            if (result == null) {
                result = GetById(id, usePrimary);

                if (result != null)
                    Cache.Set(GetScopedCacheKey(id), result);
            }

            return result;
        }

        /// <summary>
        /// Returns the IQueryable<see cref="T" />> by its given ids.
        /// </summary>
        /// <param name="ids">The string representing the ObjectId of the entities to retrieve.</param>
        /// <returns>An IQueryable<see cref="T" />> with entities that contain one of the passed in ObjectId's.</returns>
        public IQueryable<T> GetByIds(IEnumerable<string> ids) {
            return Where(Query.In(FieldNames.Id, ids.Select(id => new BsonObjectId(new ObjectId(id)))));
        }

        protected override string GetId(T entity) {
            return entity.Id;
        }

        /// <summary>
        /// Deletes an entity from the repository by its id.
        /// </summary>
        /// <param name="id">The string representation of the entity's id.</param>
        public void Delete(string id) {
            if (String.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("id");

            _collection.Remove(Query.EQ(FieldNames.Id, new BsonObjectId(new ObjectId(id))));
            InvalidateCache(GetScopedCacheKey(id));
        }

        /// <summary>
        /// Checks if the entity exists by its id.
        /// </summary>
        /// <param name="id">The string representation of the entity's id.</param>
        /// <returns>true when an entity matching the id exists, false otherwise.</returns>
        public bool Exists(string id) {
            if (String.IsNullOrWhiteSpace(id))
                throw new ArgumentNullException("id");

            return _collection.Find(Query.EQ(FieldNames.Id, new BsonObjectId(new ObjectId(id)))).Any(); // TODO: This should GetByIdCached.
        }

        protected override void ConfigureClassMap(BsonClassMap<T> cm) {
            base.ConfigureClassMap(cm);
            cm.SetIdMember(cm.GetMemberMap(c => c.Id).SetRepresentation(BsonType.ObjectId).SetIdGenerator(new StringObjectIdGenerator()));
        }
    }
}