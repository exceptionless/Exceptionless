using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Exceptionless.Core.Repositories;
using Exceptionless.Models;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public static class FindOptionsExtensions {

        public static T WithSort<T>(this T options, Expression sort) where T : OneOptions<T>, IIdentity {
            options.SortBy = sort;
            return options;
        }

        public static T WithCacheKey<T>(this T options, string cacheKey) where T : OneOptions<T>, IIdentity {
            options.CacheKey = cacheKey;
            return options;
        }

        public static T WithLimit<T>(this T options, int? limit) where T : MultiOptions<T>, IIdentity {
            options.Limit = limit;
            return options;
        }

        public static T WithExpiresAt<T>(this T options, DateTime? expiresAt) where T : OneOptions<T>, IIdentity {
            options.ExpiresAt = expiresAt;
            return options;
        }

        public static T WithExpiresIn<T>(this T options, TimeSpan? expiresIn) where T : OneOptions<T>, IIdentity {
            options.ExpiresIn = expiresIn;
            return options;
        }

        public static T WithBefore<T>(this T options, string before) where T : MultiOptions<T>, IIdentity {
            options.BeforeValue = before;
            return options;
        }

        public static T WithAfter<T>(this T options, string after) where T : MultiOptions<T>, IIdentity {
            options.AfterValue = after;
            return options;
        }

        public static T WithBeforeQuery<T>(this T options, Expression<Func<T, bool>> before) where T : MultiOptions<T>, IIdentity {
            options.BeforeQuery = before;
            return options;
        }

        public static T WithAfterQuery<T>(this T options, Expression<Func<T, bool>> after) where T : MultiOptions<T>, IIdentity {
            options.AfterQuery = after;
            return options;
        }

        public static T WithFields<T>(this T options, params string[] fields) where T : OneOptions<T>, IIdentity {
            options.Fields.AddRange(fields);
            return options;
        }

        public static T WithFields<T>(this T options, IEnumerable<string> fields) where T : OneOptions<T>, IIdentity {
            options.Fields.AddRange(fields);
            return options;
        }

        public static T WithPaging<T>(this T options, PagingOptions paging) where T : MultiOptions<T>, IIdentity {
            if (paging == null)
                return options;

            var pagingWithSorting = paging as PagingWithSortingOptions<T>;
            if (pagingWithSorting != null) {
                options.BeforeQuery = pagingWithSorting.BeforeQuery;
                options.AfterQuery = pagingWithSorting.AfterQuery;
                options.SortBy = pagingWithSorting.SortBy;
            }

            options.BeforeValue = paging.Before;
            options.AfterValue = paging.After;
            options.Page = paging.Page;
            options.Limit = paging.Limit;

            options.HasMore = false;
            options.HasMoreChanged += (source, args) => paging.HasMore = args.Value;
            return options;
        }
    }
}