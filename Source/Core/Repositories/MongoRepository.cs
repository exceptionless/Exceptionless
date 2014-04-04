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
using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using ServiceStack.CacheAccess;

namespace Exceptionless.Core {
    /// <summary>
    /// Deals with entities in MongoDb.
    /// </summary>
    /// <typeparam name="T">The type contained in the repository.</typeparam>
    public abstract class MongoRepository<T> : MongoReadOnlyRepository<T>, IRepository<T> where T : class, new() {
        protected MongoRepository(MongoDatabase database, ICacheClient cacheClient = null) : base(database, cacheClient) {}

        private static class FieldNames {
            public const string Id = "_id";
        }

        /// <summary>
        /// Adds the new entity in the repository.
        /// </summary>
        /// <param name="entity">The entity T.</param>
        /// <param name="addToCache">Add the document to the cache immediately.</param>
        /// <returns>The added entity including its new ObjectId.</returns>
        public virtual T Add(T entity, bool addToCache = false) {
            _collection.Insert<T>(entity);
            InvalidateCache(entity);
            if (addToCache && Cache != null)
                Cache.Set(GetScopedCacheKey(GetId(entity)), entity);
            return entity;
        }

        /// <summary>
        /// Adds the new entities in the repository.
        /// </summary>
        /// <param name="entities">The entities of type T.</param>
        /// <param name="addToCache">Add the documents to the cache immediately.</param>
        public virtual void Add(IEnumerable<T> entities, bool addToCache = false) {
            var list = entities.ToList();
            _collection.InsertBatch<T>(list);
            foreach (var entity in list) {
                InvalidateCache(entity);
                if (addToCache && Cache != null)
                    Cache.Set(GetScopedCacheKey(GetId(entity)), entity);
            }
        }

        /// <summary>
        /// Deletes the given entity.
        /// </summary>
        /// <param name="entity">The entity to delete.</param>
        public virtual void Delete(T entity) {
            _collection.Remove(Query.EQ(FieldNames.Id, new BsonObjectId(new ObjectId(GetId(entity)))));
            InvalidateCache(entity);
        }

        /// <summary>
        /// Deletes the entities.
        /// </summary>
        /// <param name="entities">The entities to delete.</param>
        public virtual void Delete(IEnumerable<T> entities) {
            var list = entities.ToList();
            _collection.Remove(Query.In(FieldNames.Id, list.Select(entity => new BsonObjectId(new ObjectId(GetId(entity))))));
            foreach (var entity in list)
                InvalidateCache(entity);
        }

        /// <summary>
        /// Deletes the entities matching the criteria.
        /// </summary>
        /// <param name="criteria">The expression.</param>
        public virtual void Delete(Expression<Func<T, bool>> criteria) {
            Delete(GetQueryFromExpression(criteria));
        }

        /// <summary>
        /// Deletes the entities matching the query criteria.
        /// </summary>
        /// <param name="query">The query.</param>
        public virtual void Delete(IMongoQuery query) {
            if (query != null)
                _collection.Remove(query);
        }

        /// <summary>
        /// Deletes all entities in the repository.
        /// </summary>
        public virtual void DeleteAll() {
            _collection.RemoveAll();
        }

        /// <summary>
        /// Lets the server know that this thread is done with a series of related operations.
        /// </summary>
        /// <remarks>
        /// Instead of calling this method it is better to put the return value of BeginBatch in a using statement.
        /// </remarks>
        public void EndBatch() {
            _collection.Database.RequestDone();
        }

        /// <summary>
        /// Lets the server know that this thread is about to begin a series of related operations that must all occur
        /// on the same connection. The return value of this method implements IDisposable and can be placed in a using
        /// statement (in which case EndBatch will be called automatically when leaving the using statement).
        /// </summary>
        /// <returns>A helper object that implements IDisposable and calls EndBatch() from the Dispose method.</returns>
        /// <remarks>
        /// Sometimes a series of operations needs to be performed on the same connection in order to guarantee correct
        /// results. This is rarely the case, and most of the time there is no need to call BeginBatch/EndBatch.
        /// An example of when this might be necessary is when a series of Inserts are called in rapid succession with
        /// SafeMode off, and you want to query that data in a consistent manner immediately thereafter (with SafeMode
        /// off the writes can queue up at the server and might not be immediately visible to other connections). Using
        /// BeginBatch you can force a query to be on the same connection as the writes, so the query won't execute
        /// until the server has caught up with the writes.
        /// A thread can temporarily reserve a connection from the connection pool by using BeginBatch and
        /// EndBatch. You are free to use any other databases as well during the request. BeginBatch increments a
        /// counter (for this thread) and EndBatch decrements the counter. The connection that was reserved is not
        /// actually returned to the connection pool until the count reaches zero again. This means that calls to
        /// BeginBatch/EndBatch can be nested and the right thing will happen.
        /// </remarks>
        public IDisposable BeginBatch() {
            return BeginBatch(false);
        }

        /// <summary>
        /// Lets the server know that this thread is about to begin a series of related operations that must all occur
        /// on the same connection. The return value of this method implements IDisposable and can be placed in a using
        /// statement (in which case EndBatch will be called automatically when leaving the using statement).
        /// </summary>
        /// <returns>A helper object that implements IDisposable and calls EndBatch() from the Dispose method.</returns>
        /// <param name="slaveOk">Whether queries should be sent to secondary servers.</param>
        /// <remarks>
        /// Sometimes a series of operations needs to be performed on the same connection in order to guarantee correct
        /// results. This is rarely the case, and most of the time there is no need to call BeginBatch/EndBatch.
        /// An example of when this might be necessary is when a series of Inserts are called in rapid succession with
        /// SafeMode off, and you want to query that data in a consistent manner immediately thereafter (with SafeMode
        /// off the writes can queue up at the server and might not be immediately visible to other connections). Using
        /// BeginBatch you can force a query to be on the same connection as the writes, so the query won't execute
        /// until the server has caught up with the writes.
        /// A thread can temporarily reserve a connection from the connection pool by using BeginBatch and
        /// EndBatch. You are free to use any other databases as well during the request. BeginBatch increments a
        /// counter (for this thread) and EndBatch decrements the counter. The connection that was reserved is not
        /// actually returned to the connection pool until the count reaches zero again. This means that calls to
        /// BeginBatch/EndBatch can be nested and the right thing will happen.
        /// </remarks>
        public IDisposable BeginBatch(bool slaveOk) {
            return _collection.Database.RequestStart(!slaveOk ? ReadPreference.Primary : ReadPreference.SecondaryPreferred);
        }

        /// <summary>
        /// Updates an entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="addToCache">Add the document to the cache immediately.</param>
        /// <returns>The updated entity.</returns>
        public virtual T Update(T entity, bool addToCache = false) {
            InvalidateCache(entity);
            _collection.Save<T>(entity);
            if (addToCache && Cache != null)
                Cache.Set(GetScopedCacheKey(GetId(entity)), entity);

            return entity;
        }

        /// <summary>
        /// Updates the entities.
        /// </summary>
        /// <param name="entities">The entities to update.</param>
        /// <param name="addToCache">Add the documents to the cache immediately.</param>
        public void Update(IEnumerable<T> entities, bool addToCache = false) {
            foreach (T entity in entities)
                Update(entity, addToCache);
        }
    }
}