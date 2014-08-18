using System;
using System.Collections.Generic;
using System.Linq;
using Nest;

namespace Exceptionless.Core.Repositories {
    public static class ElasticSearchOptionsExtensions {
        public static ElasticSearchOptions<T> WithFilter<T>(this ElasticSearchOptions<T> options, FilterContainer query) where T : class {
            options.Filter = query;
            return options;
        }

        public static ElasticSearchOptions<T> WithSort<T>(this ElasticSearchOptions<T> options, Func<SortFieldDescriptor<T>, IFieldSort> sort) where T : class {
            options.SortBy.Add(sort);
            return options;
        }

        public static ElasticSearchOptions<T> WithIndex<T>(this ElasticSearchOptions<T> options, string index) where T : class {
            options.Indices.Add(index);
            return options;
        }

        public static ElasticSearchOptions<T> WithIndices<T>(this ElasticSearchOptions<T> options, IEnumerable<string> indices) where T : class {
            options.Indices.AddRange(indices);
            return options;
        }

        public static FilterContainer GetElasticSearchFilter<T>(this ElasticSearchOptions<T> options) where T : class {
            var queries = GetElasticSearchFilter<T>((QueryOptions)options);

            if (!String.IsNullOrEmpty(options.BeforeValue) && options.BeforeQuery == null) {
                try {
                    options.BeforeQuery = Filter<T>.Range(r => r.OnField("_uid").Lower(options.BeforeValue));
                } catch (Exception ex) {
                    ex.ToExceptionless().AddObject(options.BeforeQuery, "BeforeQuery").Submit();
                }
            }

            if (!String.IsNullOrEmpty(options.AfterValue) && options.AfterQuery == null) {
                try {
                    options.AfterQuery = Filter<T>.Range(r => r.OnField("_uid").Greater(options.AfterValue));
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

        public static FilterContainer GetElasticSearchFilter<T>(this QueryOptions options) where T : class {
            var queries = Filter<T>.MatchAll();
            
            if (options.Ids.Count > 0)
                queries &= Filter<T>.Ids(options.Ids);
            
            if (options.OrganizationIds.Count > 0) {
                if (options.OrganizationIds.Count == 1)
                    queries &= Filter<T>.Term("organization_id", options.OrganizationIds.First());
                else
                    queries &= Filter<T>.Terms("organization_id", options.OrganizationIds.ToArray());
            }

            if (options.ProjectIds.Count > 0) {
                if (options.ProjectIds.Count == 1)
                    queries &= Filter<T>.Term("project_id", options.ProjectIds.First());
                else
                    queries &= Filter<T>.Terms("project_id", options.ProjectIds.ToArray());
            }

            if (options.StackIds.Count > 0) {
                if (options.StackIds.Count == 1)
                    queries &= Filter<T>.Term("stack_id", options.StackIds.First());
                else
                    queries &= Filter<T>.Terms("stack_id", options.StackIds.ToArray());
            }

            var elasticSearchOptions = options as ElasticSearchOptions<T>;
            if (elasticSearchOptions != null && elasticSearchOptions.Filter != null)
                 queries &= elasticSearchOptions.Filter;

            return queries;
        }

        public static ElasticSearchOptions<T> WithPaging<T>(this ElasticSearchOptions<T> options, PagingOptions paging) where T : class {
            if (paging == null)
                return options;
            
            var elasticSearchPagingOptions = paging as ElasticSearchPagingOptions<T>;
            if (elasticSearchPagingOptions != null) {
                options.BeforeQuery = elasticSearchPagingOptions.BeforeFilter;
                options.AfterQuery = elasticSearchPagingOptions.AfterFilter;
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