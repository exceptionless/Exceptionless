using System;
using System.Collections.Generic;
using System.Linq;
using Nest;

namespace Exceptionless.Core.Repositories {
    public static class ElasticSearchOptionsExtensions {
        public static ElasticSearchOptions<T> WithQuery<T>(this ElasticSearchOptions<T> options, QueryContainer query) where T : class {
            options.Query = query;
            return options;
        }

        public static ElasticSearchOptions<T> WithSort<T>(this ElasticSearchOptions<T> options, Func<SortFieldDescriptor<T>, IFieldSort> sort) where T : class {
            options.SortBy = sort;
            return options;
        }

        public static QueryContainer GetElasticSearchQuery<T>(this ElasticSearchOptions<T> options) where T : class {
            var queries = new List<QueryContainer>();

            var query = GetElasticSearchQuery<T>((QueryOptions)options);
            if (query != null)
                queries.Add(query);

            if (!String.IsNullOrEmpty(options.BeforeValue) && options.BeforeQuery == null) {
                try {
                    options.BeforeQuery = Query<T>.Range(r => r.OnField("id").Lower(options.BeforeValue));
                    queries.Add(options.BeforeQuery);
                } catch (Exception ex) {
                    ex.ToExceptionless().AddObject(options.BeforeQuery, "BeforeQuery").Submit();
                }
            }

            if (!String.IsNullOrEmpty(options.AfterValue) && options.AfterQuery == null) {
                try {
                    options.AfterQuery = Query<T>.Range(r => r.OnField("id").Greater(options.AfterValue));
                    queries.Add(options.AfterQuery);
                } catch (Exception ex) {
                    ex.ToExceptionless().AddObject(options.AfterQuery, "AfterQuery").Submit();
                }
            }

            return queries.Count > 0 ? Query<T>.Bool(b => b.Must(queries.ToArray())) : null;
        }

        public static QueryContainer GetElasticSearchQuery<T>(this QueryOptions options) where T : class {
            var queries = new List<QueryContainer>();
            if (options.Ids.Count > 0)
                queries.Add(Query<T>.Ids(options.Ids));

            if (options.OrganizationIds.Count > 0) {
                if (options.OrganizationIds.Count == 1)
                     queries.Add(Query<T>.Bool(b => b.Must(m => m.Term("organization_id", options.OrganizationIds.First()))));
                else
                     queries.Add(Query<T>.Bool(b => b.Must(m => m.Terms("organization_id", options.OrganizationIds))));
            }

            if (options.ProjectIds.Count > 0) {
                if (options.ProjectIds.Count == 1)
                     queries.Add(Query<T>.Bool(b => b.Must(m => m.Term("project_id", options.ProjectIds.First()))));
                else
                     queries.Add(Query<T>.Bool(b => b.Must(m => m.Terms("project_id", options.ProjectIds))));
            }

            if (options.StackIds.Count > 0) {
                if (options.StackIds.Count == 1)
                     queries.Add(Query<T>.Bool(b => b.Must(m => m.Term("stack_id", options.StackIds.First()))));
                else
                     queries.Add(Query<T>.Bool(b => b.Must(m => m.Terms("stack_id", options.StackIds))));
            }

            var elasticSearchOptions = options as ElasticSearchOptions<T>;
            if (elasticSearchOptions != null && elasticSearchOptions.Query != null)
                 queries.Add(elasticSearchOptions.Query);

            return queries.Count > 0 ? Query<T>.Bool(b => b.Must(queries.ToArray())) : null;
        }

        public static ElasticSearchOptions<T> WithPaging<T>(this ElasticSearchOptions<T> options, PagingOptions paging) where T : class {
            if (paging == null)
                return options;

            var elasticSearchPagingOptions = paging as ElasticSearchPagingOptions<T>;
            if (elasticSearchPagingOptions != null) {
                options.BeforeQuery = elasticSearchPagingOptions.BeforeQuery;
                options.AfterQuery = elasticSearchPagingOptions.AfterQuery;
                options.SortBy = elasticSearchPagingOptions.SortBy;
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