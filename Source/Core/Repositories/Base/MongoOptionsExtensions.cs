using System;
using System.Linq;
using Exceptionless.Core.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Repositories {
    public static class MongoOptionsExtensions {
        public static T WithQuery<T>(this T options, IMongoQuery query) where T : MongoOptions {
            options.Query = query;
            return options;
        }

        public static T WithReadPreference<T>(this T options, ReadPreference readPreference) where T : MongoOptions {
            options.ReadPreference = readPreference;
            return options;
        }
        
        public static T WithSort<T>(this T options, IMongoSortBy sort) where T : MongoOptions {
            options.SortBy = sort;
            return options;
        }

        public static IMongoQuery GetMongoQuery(this MultiOptions options, Func<string, BsonValue> getIdValue = null) {
            var query = GetMongoQuery((QueryOptions)options, getIdValue);
            var mongoOptions = options as MongoOptions;
            if (mongoOptions == null)
                return query;

            if (getIdValue == null)
                getIdValue = id => new BsonObjectId(new ObjectId(id));

            if (options.UseDateRange)
                query = query.And(Query.GTE(options.DateField, options.GetStartDate()).And(Query.LTE(options.DateField, options.GetEndDate())));

            return query;
        }

        public static IMongoQuery GetMongoQuery(this QueryOptions options, Func<string, BsonValue> getIdValue = null) {
            if (getIdValue == null)
                getIdValue = id => new BsonObjectId(new ObjectId(id));

            var mongoOptions = options as MongoOptions;
            IMongoQuery query = mongoOptions != null ? mongoOptions.Query : Query.Null;
            if (options.Ids.Count > 0) {
                if (options.Ids.Count == 1)
                    query = query.And(Query.EQ(CommonFieldNames.Id, getIdValue(options.Ids.First())));
                else
                    query = query.And(Query.In(CommonFieldNames.Id, options.Ids.Select(id => getIdValue(id))));
            }

            if (options.OrganizationIds.Count > 0) {
                if (options.OrganizationIds.Count == 1)
                    query = query.And(Query.EQ(CommonFieldNames.OrganizationId, new BsonObjectId(new ObjectId(options.OrganizationIds.First()))));
                else
                    query = query.And(Query.In(CommonFieldNames.OrganizationId, options.OrganizationIds.Select(id => new BsonObjectId(new ObjectId(id)))));
            }

            if (options.ProjectIds.Count > 0) {
                if (options.ProjectIds.Count == 1)
                    query = query.And(Query.EQ(CommonFieldNames.ProjectId, new BsonObjectId(new ObjectId(options.ProjectIds.First()))));
                else
                    query = query.And(Query.In(CommonFieldNames.ProjectId, options.ProjectIds.Select(id => new BsonObjectId(new ObjectId(id)))));
            }

            if (options.StackIds.Count > 0) {
                if (options.StackIds.Count == 1)
                    query = query.And(Query.EQ(CommonFieldNames.StackId, new BsonObjectId(new ObjectId(options.StackIds.First()))));
                else
                    query = query.And(Query.In(CommonFieldNames.StackId, options.StackIds.Select(id => new BsonObjectId(new ObjectId(id)))));
            }

            return query;
        }
    
        public static MongoOptions WithPaging<T>(this MongoOptions options, PagingOptions paging) where T : MultiOptions {
            if (paging == null)
                return options;

            var mongoPagingOptions = paging as MongoPagingOptions;
            if (mongoPagingOptions != null) {
                options.SortBy = mongoPagingOptions.SortBy;
            }

            options.Page = paging.Page;
            options.Limit = paging.Limit;

            options.HasMore = false;
            options.HasMoreChanged += (source, args) => paging.HasMore = args.Value;
            return options;
        }
    }
}