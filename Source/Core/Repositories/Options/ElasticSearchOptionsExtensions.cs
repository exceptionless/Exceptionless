using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.DateTimeExtensions;
using Nest;

namespace Exceptionless.Core.Repositories {
    public static class ElasticSearchOptionsExtensions {
        public static ElasticSearchOptions<T> WithFilter<T>(this ElasticSearchOptions<T> options, FilterContainer filter) where T : class {
            options.Filter = filter;
            return options;
        }

        public static ElasticSearchOptions<T> WithFilter<T>(this ElasticSearchOptions<T> options, string filter) where T : class {
            options.Filter = !String.IsNullOrEmpty(filter) ? Filter<T>.Query(q => q.QueryString(qs => qs.DefaultOperator(Operator.And).Query(filter))) : null;
            return options;
        }

        public static ElasticSearchOptions<T> WithSystemFilter<T>(this ElasticSearchOptions<T> options, string filter) where T : class {
            options.SystemFilter = !String.IsNullOrEmpty(filter) ? Filter<T>.Query(q => q.QueryString(qs => qs.DefaultOperator(Operator.And).Query(filter))) : null;
            return options;
        }

        public static ElasticSearchOptions<T> WithSystemFilter<T>(this ElasticSearchOptions<T> options, FilterContainer filter) where T : class {
            options.SystemFilter = filter;
            return options;
        }

        public static ElasticSearchOptions<T> WithQuery<T>(this ElasticSearchOptions<T> options, string query, bool useAndAsDefaultOperator = true) where T : class {
            options.Query = query;
            options.DefaultQueryOperator = useAndAsDefaultOperator ? Operator.And : Operator.Or;
            return options;
        }

        public static ElasticSearchOptions<T> WithSort<T>(this ElasticSearchOptions<T> options, string sort, SortOrder sortOrder) where T : class {
            if (!String.IsNullOrEmpty(sort))
                options.WithSort(e => e.OnField(sort).Order(sortOrder == SortOrder.Descending ? Nest.SortOrder.Descending : Nest.SortOrder.Ascending));

            return options;
        }

        public static ElasticSearchOptions<T> WithSort<T>(this ElasticSearchOptions<T> options, Func<SortFieldDescriptor<T>, IFieldSort> sort) where T : class {
            options.SortBy.Add(sort);
            return options;
        }

        public static FilterContainer GetElasticSearchFilter<T>(this ElasticSearchOptions<T> options, bool supportSoftDeletes = false) where T : class {
            var container = Filter<T>.MatchAll();

            container = ApplyQueryOptionsFilters<T>(options, container, supportSoftDeletes);
            container = ApplyElasticSearchOptionsFilters(options, container);

            return container;
        }

        public static FilterContainer GetElasticSearchFilter<T>(this QueryOptions options, bool supportSoftDeletes = false) where T : class {
            var container = Filter<T>.MatchAll();

            container = ApplyQueryOptionsFilters<T>(options, container, supportSoftDeletes);

            var elasticSearchOptions = options as ElasticSearchOptions<T>;
            if (elasticSearchOptions != null)
                container = ApplyElasticSearchOptionsFilters(elasticSearchOptions, container);

            return container;
        }

        private static FilterContainer ApplyQueryOptionsFilters<T>(QueryOptions options, FilterContainer container, bool supportSoftDeletes = false) where T : class {
            if (container == null)
                container = Filter<T>.MatchAll();

            if (options.Ids.Count > 0)
                container &= Filter<T>.Ids(options.Ids);

            if (options.OrganizationIds.Count > 0) {
                if (options.OrganizationIds.Count == 1)
                    container &= Filter<T>.Term("organization", options.OrganizationIds.First());
                else
                    container &= Filter<T>.Terms("organization", options.OrganizationIds.ToArray());
            }

            if (options.ProjectIds.Count > 0) {
                if (options.ProjectIds.Count == 1)
                    container &= Filter<T>.Term("project", options.ProjectIds.First());
                else
                    container &= Filter<T>.Terms("project", options.ProjectIds.ToArray());
            }

            if (options.StackIds.Count > 0) {
                if (options.StackIds.Count == 1)
                    container &= Filter<T>.Term("stack", options.StackIds.First());
                else
                    container &= Filter<T>.Terms("stack", options.StackIds.ToArray());
            }

            return container;
        }

        private static FilterContainer ApplyElasticSearchOptionsFilters<T>(ElasticSearchOptions<T> options, FilterContainer container, bool isQuery = false) where T : class {
            if (container == null)
                container = Filter<T>.MatchAll();

            if (options != null && options.SystemFilter != null)
                container &= options.SystemFilter;

            if (options != null && options.Filter != null)
                container &= options.Filter;

            if (options != null && options.UseDateRange)
                container &= Filter<T>.Range(r => r.OnField(options.DateField).GreaterOrEquals(options.GetStartDate()).LowerOrEquals(options.GetEndDate()));

            if (options != null && !String.IsNullOrEmpty(options.BeforeValue) && options.BeforeQuery == null)
                options.BeforeQuery = Filter<T>.Range(r => r.OnField("_uid").Lower(options.BeforeValue));

            if (options != null && !String.IsNullOrEmpty(options.AfterValue) && options.AfterQuery == null)
                options.AfterQuery = Filter<T>.Range(r => r.OnField("_uid").Greater(options.AfterValue));

            if (options != null && options.BeforeQuery != null)
                container &= options.BeforeQuery;

            if (options != null && options.AfterQuery != null)
                container &= options.AfterQuery;

            if (!isQuery && options != null && !String.IsNullOrEmpty(options.Query))
                container &= Filter<T>.Query(q => q.QueryString(qs => qs.DefaultOperator(options.DefaultQueryOperator).Query(options.Query).AnalyzeWildcard()));

            return container;
        }

        public static QueryContainer GetElasticSearchQuery<T>(this ElasticSearchOptions<T> options, bool supportSoftDeletes = false) where T : class {
            var container = Query<T>.MatchAll();

            var filterContainer = Filter<T>.MatchAll();
            filterContainer = ApplyQueryOptionsFilters<T>(options, filterContainer, supportSoftDeletes);
            filterContainer = ApplyElasticSearchOptionsFilters(options, filterContainer, true);

            container &= Query<T>.Filtered(f => f.Filter(d => filterContainer));

            if (options != null && !String.IsNullOrEmpty(options.Query))
                container &= Query<T>.QueryString(qs => qs.DefaultOperator(options.DefaultQueryOperator).Query(options.Query).AnalyzeWildcard());

            return container;
        }

        public static QueryContainer GetElasticSearchQuery<T>(this QueryOptions options, bool supportSoftDeletes = false) where T : class {
            var container = Query<T>.MatchAll();

            var filterContainer = Filter<T>.MatchAll();
            filterContainer = ApplyQueryOptionsFilters<T>(options, filterContainer, supportSoftDeletes);

            var elasticSearchOptions = options as ElasticSearchOptions<T>;
            if (elasticSearchOptions != null)
                filterContainer = ApplyElasticSearchOptionsFilters(elasticSearchOptions, filterContainer);

            container &= Query<T>.Filtered(f => f.Filter(d => filterContainer));

            return container;
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

        public static ElasticSearchOptions<T> WithIndex<T>(this ElasticSearchOptions<T> options, string index) where T : class {
            options.Indices.Add(index);
            return options;
        }

        public static ElasticSearchOptions<T> WithIndices<T>(this ElasticSearchOptions<T> options, IEnumerable<string> indices) where T : class {
            options.Indices.AddRange(indices);
            return options;
        }

        public static ElasticSearchOptions<T> WithIndices<T>(this ElasticSearchOptions<T> options, DateTime? utcStart, DateTime? utcEnd, string nameFormat = null) where T : class {
            options.Indices.AddRange(GetTargetIndex<T>(utcStart, utcEnd, nameFormat));
            return options;
        }

        public static ElasticSearchOptions<T> WithIndicesFromDateRange<T>(this ElasticSearchOptions<T> options, string nameFormat = null) where T : class {
            if (!options.UseDateRange)
                return options;

            options.Indices.AddRange(GetTargetIndex<T>(options.GetStartDate(), options.GetEndDate(), nameFormat));
            return options;
        }

        private static IEnumerable<string> GetTargetIndex<T>(DateTime? utcStart, DateTime? utcEnd, string nameFormat = null) {
            if (!utcStart.HasValue)
                utcStart = DateTime.UtcNow;

            if (!utcEnd.HasValue || utcEnd.Value < utcStart)
                utcEnd = DateTime.UtcNow;

            if (String.IsNullOrEmpty(nameFormat))
                nameFormat = $"'{typeof(T).Name.ToLower()}-'yyyyMM";

            // Use the end of the month as we are using monthly indexes.
            var utcEndOfMonth = utcEnd.Value.EndOfMonth();

            var indices = new List<string>();
            for (DateTime current = utcStart.Value; current <= utcEndOfMonth; current = current.AddMonths(1)) {
                indices.Add(current.ToString(nameFormat));
            }

            return indices;
        }
    }
}