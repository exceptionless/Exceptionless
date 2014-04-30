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
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core {
    /// <summary>
    /// Deals with entities in MongoDb.
    /// </summary>
    /// <typeparam name="T">The type contained in the repository.</typeparam>
    public abstract class MongoRepositoryOwnedByOrganization<T> : MongoRepositoryWithIdentity<T>, IRepositoryOwnedByOrganization<T> where T : class, IOwnedByOrganization, IIdentity, new() {
        protected MongoRepositoryOwnedByOrganization(MongoDatabase database, ICacheClient cacheClient = null) : base(database, cacheClient) {}

        public new static class FieldNames {
            public const string OrganizationId = "oid";
        }

        public IQueryable<T> GetByOrganizationId(string id) {
            // TODO: Cache this.
            return Where(Query.EQ(FieldNames.OrganizationId, new BsonObjectId(new ObjectId(id))));
        }

        public IQueryable<T> GetByOrganizationIds(IEnumerable<string> ids) {
            // TODO: Cache this.
            return Where(Query.In(FieldNames.OrganizationId, ids.Select(id => new BsonObjectId(new ObjectId(id)))));
        }

        protected override void ConfigureClassMap(BsonClassMap<T> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(p => p.OrganizationId).SetElementName(FieldNames.OrganizationId).SetRepresentation(BsonType.ObjectId);
        }
    }
}