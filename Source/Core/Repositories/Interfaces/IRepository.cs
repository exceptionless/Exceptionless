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
using System.Linq.Expressions;
using MongoDB.Driver;

namespace Exceptionless.Core {
    /// <summary>
    /// IRepository definition.
    /// </summary>
    /// <typeparam name="T">The type contained in the repository.</typeparam>
    public interface IRepository<T> : IReadOnlyRepository<T> where T : class, new() {
        /// <summary>
        /// Adds the new entity in the repository.
        /// </summary>
        /// <param name="entity">The entity to add.</param>
        /// <returns>The added entity including its new ObjectId.</returns>
        T Add(T entity);

        /// <summary>
        /// Adds the new entities in the repository.
        /// </summary>
        /// <param name="entities">The entities of type T.</param>
        void Add(IEnumerable<T> entities);

        /// <summary>
        /// Updates an entity.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>The updated entity.</returns>
        T Update(T entity);

        /// <summary>
        /// Updates the entities.
        /// </summary>
        /// <param name="entities">The entities to update.</param>
        void Update(IEnumerable<T> entities);

        /// <summary>
        /// Deletes the given entity.
        /// </summary>
        /// <param name="entity">The entity to delete.</param>
        void Delete(T entity);

        /// <summary>
        /// Deletes the entities.
        /// </summary>
        /// <param name="entities">The entities to delete.</param>
        void Delete(IEnumerable<T> entities);

        /// <summary>
        /// Deletes the entities matching the criteria.
        /// </summary>
        /// <param name="criteria">The expression.</param>
        void Delete(Expression<Func<T, bool>> criteria);

        /// <summary>
        /// Deletes the entities matching the query criteria.
        /// </summary>
        /// <param name="query">The query.</param>
        void Delete(IMongoQuery query);

        /// <summary>
        /// Deletes all entities in the repository.
        /// </summary>
        void DeleteAll();

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
        IDisposable BeginBatch();

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
        IDisposable BeginBatch(bool slaveOk);

        /// <summary>
        /// Lets the server know that this thread is done with a series of related operations.
        /// </summary>
        /// <remarks>
        /// Instead of calling this method it is better to put the return value of BeginBatch in a using statement.
        /// </remarks>
        void EndBatch();
    }
}