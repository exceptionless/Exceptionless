using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Exceptionless.Core.Repositories {
    public class QueryOptions {
        public QueryOptions() {
            Ids = new List<string>();
            OrganizationIds = new List<string>();
            ProjectIds = new List<string>();
            StackIds = new List<string>();
        }

        public List<string> Ids { get; set; }
        public List<string> OrganizationIds { get; set; }
        public List<string> ProjectIds { get; set; }
        public List<string> StackIds { get; set; }
        public IMongoQuery Query { get; set; }

        public virtual IMongoQuery GetQuery(Func<string, BsonValue> getIdValue = null) {
            if (getIdValue == null)
                getIdValue = id => new BsonObjectId(new ObjectId(id));

            IMongoQuery query = Query;
            if (Ids.Count > 0)
                query = query.And(MongoDB.Driver.Builders.Query.In(CommonFieldNames.Id, Ids.Select(id => getIdValue(id))));
            if (OrganizationIds.Count > 0)
                query = query.And(MongoDB.Driver.Builders.Query.In(CommonFieldNames.OrganizationId, OrganizationIds.Select(id => new BsonObjectId(new ObjectId(id)))));
            if (ProjectIds.Count > 0)
                query = query.And(MongoDB.Driver.Builders.Query.In(CommonFieldNames.ProjectId, ProjectIds.Select(id => new BsonObjectId(new ObjectId(id)))));
            if (StackIds.Count > 0)
                query = query.And(MongoDB.Driver.Builders.Query.In(CommonFieldNames.StackId, StackIds.Select(id => new BsonObjectId(new ObjectId(id)))));

            return query;
        }
    }
}
