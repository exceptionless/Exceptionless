using System;
using System.Linq;
using Nest;

namespace Exceptionless.Core.Repositories {
    public static class ElasticSearchOptionsExtensions {
        public static ElasticSearchOptions<T> WithQuery<T>(this ElasticSearchOptions<T> options, QueryContainer query) where T : class {
            options.Query = query;
            return options;
        }

        public static ElasticSearchOptions<T> WithSort<T>(this ElasticSearchOptions<T> options, Func<SortFieldDescriptor<T>, IFieldSort> sort) where T : class {
            options.SortBy.Add(sort);
            return options;
        }

        public static QueryContainer GetElasticSearchQuery<T>(this ElasticSearchOptions<T> options) where T : class {
            var queries = GetElasticSearchQuery<T>((QueryOptions)options);

            if (!String.IsNullOrEmpty(options.BeforeValue) && options.BeforeQuery == null) {
                try {
                    options.BeforeQuery = Query<T>.Range(r => r.OnField("id").Lower(options.BeforeValue));
                } catch (Exception ex) {
                    ex.ToExceptionless().AddObject(options.BeforeQuery, "BeforeQuery").Submit();
                }
            }

            if (!String.IsNullOrEmpty(options.AfterValue) && options.AfterQuery == null) {
                try {
                    options.AfterQuery = Query<T>.Range(r => r.OnField("id").Greater(options.AfterValue));
                } catch (Exception ex) {
                    ex.ToExceptionless().AddObject(options.AfterQuery, "AfterQuery").Submit();
                }
            }

            if (options.BeforeQuery != null)
                queries &= options.BeforeQuery;
            if (options.AfterQuery != null)
                queries &= options.AfterQuery;

            return queries;
        }

        public static QueryContainer GetElasticSearchQuery<T>(this QueryOptions options) where T : class {
            var queries = Query<T>.MatchAll();

            if (options.Ids.Count > 0)
                queries &= Query<T>.Ids(options.Ids);
            
            if (options.OrganizationIds.Count > 0) {
                if (options.OrganizationIds.Count == 1)
                     queries &= Query<T>.Term("organization_id", options.OrganizationIds.First());
                else
                     queries &= Query<T>.Terms("organization_id", options.OrganizationIds.ToArray());
            }

            if (options.ProjectIds.Count > 0) {
                if (options.ProjectIds.Count == 1)
                     queries &= Query<T>.Term("project_id", options.ProjectIds.First());
                else
                     queries &= Query<T>.Terms("project_id", options.ProjectIds.ToArray());
            }

            if (options.StackIds.Count > 0) {
                if (options.StackIds.Count == 1)
                     queries &= Query<T>.Term("stack_id", options.StackIds.First());
                else
                     queries &= Query<T>.Terms("stack_id", options.StackIds.ToArray());
            }

            var elasticSearchOptions = options as ElasticSearchOptions<T>;
            if (elasticSearchOptions != null && elasticSearchOptions.Query != null)
                 queries &= elasticSearchOptions.Query;

            return queries;
        }

        public static ElasticSearchOptions<T> WithPaging<T>(this ElasticSearchOptions<T> options, PagingOptions paging) where T : class {
            if (paging == null)
                return options;
            
            var elasticSearchPagingOptions = paging as ElasticSearchPagingOptions<T>;
            if (elasticSearchPagingOptions != null) {
                options.BeforeQuery = elasticSearchPagingOptions.BeforeQuery;
                options.AfterQuery = elasticSearchPagingOptions.AfterQuery;
                options.SortBy.AddRange(elasticSearchPagingOptions.SortBy);
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