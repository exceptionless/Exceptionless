using System;
using System.Collections.Generic;
using System.Linq;
using CodeSmith.Core.Extensions;
using Exceptionless.Models;
using Nest;

namespace Exceptionless.Core.Repositories {
    public static class ElasticSearchOptionsExtensions {
        public static ElasticSearchOptions<T> WithFilter<T>(this ElasticSearchOptions<T> options, FilterContainer filter) where T : class {
            options.Filter = filter;
            return options;
        }

        public static ElasticSearchOptions<T> WithQuery<T>(this ElasticSearchOptions<T> options, string query) where T : class {
            options.Query = query;
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

        public static ElasticSearchOptions<T> WithIndices<T>(this ElasticSearchOptions<T> options, DateTime? utcStart, DateTime? utcEnd) where T : class {
            options.Indices.AddRange(GetTargetIndex(utcStart, utcEnd));
            return options;
        }

        public static ElasticSearchOptions<T> WithIndicesFromDateRange<T>(this ElasticSearchOptions<T> options) where T : PersistentEvent {
            if (!options.UseDateRange)
                return options;

            options.Indices.AddRange(GetTargetIndex(options.GetStartDate(), options.GetEndDate()));
            return options;
        }

        private static IEnumerable<string> GetTargetIndex(DateTime? utcStart, DateTime? utcEnd) {
            if (!utcStart.HasValue || utcStart < MultiOptions.ServiceStartDate)
                utcStart = MultiOptions.ServiceStartDate;

            if (!utcEnd.HasValue || utcEnd.Value > DateTime.UtcNow.AddHours(1))
                utcEnd = DateTime.UtcNow.AddHours(1);

            var current = new DateTime(utcStart.Value.Year, utcStart.Value.Month, 1);
            var indices = new List<string>();
            while (current <= utcEnd) {
                indices.Add("events-v1-" + current.ToString("yyyyMM"));
                current = current.AddMonths(1);
            }

            return indices;
        }

        public static FilterContainer GetElasticSearchFilter<T>(this ElasticSearchOptions<T> options) where T : class {
            var queries = GetElasticSearchFilter<T>((QueryOptions)options);

            if (options.UseDateRange)
                queries &= Filter<T>.Range(r => r.OnField(options.DateField).GreaterOrEquals(options.GetStartDate()).LowerOrEquals(options.GetEndDate()));

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
            if (elasticSearchOptions != null && !String.IsNullOrEmpty(elasticSearchOptions.Query))
                queries &= Filter<T>.Query(q => q.QueryString(qs => qs.DefaultOperator(Operator.And).Query(elasticSearchOptions.Query).AnalyzeWildcard()));

            return queries;
        }

        public static ElasticSearchOptions<T> WithPaging<T>(this ElasticSearchOptions<T> options, PagingOptions paging) where T : class {
            if (paging == null)
                return options;
            
            var elasticSearchPagingOptions = paging as ElasticSearchPagingOptions<T>;
            if (elasticSearchPagingOptions != null) {
                options.SortBy.AddRange(elasticSearchPagingOptions.SortBy);
            }

            options.Page = paging.Page;
            options.Limit = paging.Limit;

            options.HasMore = false;
            options.HasMoreChanged += (source, args) => paging.HasMore = args.Value;
            return options;
        }
    }
}