using System;
using System.Collections.Generic;
using MongoDB.Driver;

namespace Exceptionless.Core.Repositories {
    public static class FindOptionsExtensions {
        public static T WithId<T>(this T options, string id) where T: QueryOptions {
            options.Ids.Add(id);
            return options;
        }

        public static T WithIds<T>(this T options, params string[] ids) where T : QueryOptions {
            options.Ids.AddRange(ids);
            return options;
        }

        public static T WithIds<T>(this T options, IEnumerable<string> ids) where T : QueryOptions {
            options.Ids.AddRange(ids);
            return options;
        }

        public static T WithOrganizationId<T>(this T options, string id) where T : QueryOptions {
            options.OrganizationIds.Add(id);
            return options;
        }

        public static T WithOrganizationIds<T>(this T options, params string[] ids) where T : QueryOptions {
            options.OrganizationIds.AddRange(ids);
            return options;
        }

        public static T WithOrganizationIds<T>(this T options, IEnumerable<string> ids) where T : QueryOptions {
            options.OrganizationIds.AddRange(ids);
            return options;
        }

        public static T WithProjectId<T>(this T options, string id) where T : QueryOptions {
            options.ProjectIds.Add(id);
            return options;
        }

        public static T WithProjectIds<T>(this T options, params string[] ids) where T : QueryOptions {
            options.ProjectIds.AddRange(ids);
            return options;
        }

        public static T WithProjectIds<T>(this T options, IEnumerable<string> ids) where T : QueryOptions {
            options.ProjectIds.AddRange(ids);
            return options;
        }

        public static T WithStackId<T>(this T options, string id) where T : QueryOptions {
            options.StackIds.Add(id);
            return options;
        }

        public static T WithStackIds<T>(this T options, params string[] ids) where T : QueryOptions {
            options.StackIds.AddRange(ids);
            return options;
        }

        public static T WithStackIds<T>(this T options, IEnumerable<string> ids) where T : QueryOptions {
            options.StackIds.AddRange(ids);
            return options;
        }

        public static T WithQuery<T>(this T options, IMongoQuery query) where T : QueryOptions {
            options.Query = query;
            return options;
        }

        public static T WithSort<T>(this T options, IMongoSortBy sort) where T : FindOptions {
            options.SortBy = sort;
            return options;
        }

        public static T WithCacheKey<T>(this T options, string cacheKey) where T: FindOptions {
            options.CacheKey = cacheKey;
            return options;
        }

        public static T WithLimit<T>(this T options, int? limit) where T : FindMultipleOptions {
            options.Limit = limit;
            return options;
        }

        public static T WithExpiresAt<T>(this T options, DateTime? expiresAt) where T: FindOptions {
            options.ExpiresAt = expiresAt;
            return options;
        }

        public static T WithExpiresIn<T>(this T options, TimeSpan? expiresIn) where T: FindOptions {
            options.ExpiresIn = expiresIn;
            return options;
        }

        public static T WithBefore<T>(this T options, string before) where T : FindMultipleOptions {
            options.BeforeValue = before;
            return options;
        }

        public static T WithAfter<T>(this T options, string after) where T : FindMultipleOptions {
            options.AfterValue = after;
            return options;
        }

        public static T WithBeforeQuery<T>(this T options, IMongoQuery before) where T : FindMultipleOptions {
            options.BeforeQuery = before;
            return options;
        }

        public static T WithAfterQuery<T>(this T options, IMongoQuery after) where T : FindMultipleOptions {
            options.AfterQuery = after;
            return options;
        }

        public static T WithFields<T>(this T options, params string[] fields) where T : FindMultipleOptions {
            options.Fields.AddRange(fields);
            return options;
        }

        public static T WithFields<T>(this T options, IEnumerable<string> fields) where T : FindMultipleOptions {
            options.Fields.AddRange(fields);
            return options;
        }

        public static T WithPaging<T>(this T options, PagingOptions paging) where T : FindMultipleOptions {
            options.AfterValue = paging.After;
            options.BeforeValue = paging.Before;
            options.Page = paging.Page;
            options.Limit = paging.Limit;
            options.HasMoreChanged += (source, args) => paging.HasMore = args.Value;
            return options;
        }
    }
}