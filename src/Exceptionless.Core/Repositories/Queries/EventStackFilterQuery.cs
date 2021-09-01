using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Core.Repositories.Options;
using Foundatio.Caching;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Foundatio.Parsers.LuceneQueries.Extensions;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Options;
using Microsoft.Extensions.Logging;
using Nest;
using DateRange = Foundatio.Repositories.DateRange;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Repositories {
    public static class EventStackFilterQueryExtensions {
        internal const string EnforceEventStackFilterKey = "@EnforceEventStackFilter";

        public static T EnforceEventStackFilter<T>(this T query, bool shouldEnforceEventStackFilter = true) where T : IRepositoryQuery {
            query.Values.Set(EnforceEventStackFilterKey, shouldEnforceEventStackFilter);
            return query;
        }

        internal const string EventStackFilterInvertedKey = "@IsStackFilterInverted";

        public static T EventStackFilterInverted<T>(this T query, bool eventStackFilterInverted = true) where T : IRepositoryQuery {
            query.Values.Set(EventStackFilterInvertedKey, eventStackFilterInverted);
            return query;
        }
    }
}

namespace Exceptionless.Core.Repositories.Options {
    public static class ReadEventStackFilterQueryExtensions {
        public static bool ShouldEnforceEventStackFilter(this IRepositoryQuery query) {
            return query.SafeGetOption<bool>(EventStackFilterQueryExtensions.EnforceEventStackFilterKey, false);
        }

        public static bool IsEventStackFilterInverted(this IRepositoryQuery query) {
            return query.SafeGetOption<bool>(EventStackFilterQueryExtensions.EventStackFilterInvertedKey, false);
        }
    }
}

namespace Exceptionless.Core.Repositories.Queries {
    public class EventStackFilterQueryBuilder : IElasticQueryBuilder {
        private readonly IStackRepository _stackRepository;
        private readonly ILogger _logger;
        private readonly Field _inferredEventDateField;
        private readonly Field _inferredStackLastOccurrenceField;
        private readonly EventStackFilter _eventStackFilter;
        private readonly ICacheClient _cacheClient;

        public EventStackFilterQueryBuilder(IStackRepository stackRepository, ICacheClient cacheClient, ILoggerFactory loggerFactory) {
            _stackRepository = stackRepository;
            _cacheClient = new ScopedCacheClient(cacheClient, "stack-filter");
            _logger = loggerFactory.CreateLogger<EventStackFilterQueryBuilder>();
            _inferredEventDateField = Infer.Field<PersistentEvent>(f => f.Date);
            _inferredStackLastOccurrenceField = Infer.Field<Stack>(f => f.LastOccurrence);
            _eventStackFilter = new EventStackFilter();
        }

        public async Task BuildAsync<T>(QueryBuilderContext<T> ctx) where T : class, new() {
            if (!ctx.Source.ShouldEnforceEventStackFilter())
                return;

            // TODO: Handle search expressions as well
            string filter = ctx.Source.GetFilterExpression() ?? String.Empty;
            bool altInvertRequested = false;
            if (filter.StartsWith("@!")) {
                altInvertRequested = true;
                filter = filter.Substring(2);
                ctx.Source.FilterExpression(filter);
            }

            // when inverting to get excluded stack ids, add is_deleted as an alternate inverted criteria
            if (ctx.Options.GetSoftDeleteMode() == SoftDeleteQueryMode.ActiveOnly)
                ctx.SetAlternateInvertedCriteria(new TermNode { Field = "is_deleted", Term = "true" });

            var stackFilter = await _eventStackFilter.GetStackFilterAsync(filter, ctx);

            const int stackIdLimit = 10000;
            string[] stackIds = new string[0];
            long stackTotal = 0;

            string stackFilterValue = stackFilter.Filter;
            bool isStackIdsNegated = stackFilter.HasStatusOpen && !altInvertRequested;
            if (isStackIdsNegated)
                stackFilterValue = stackFilter.InvertedFilter;

            if (String.IsNullOrEmpty(stackFilterValue) && (!ctx.Source.ShouldEnforceEventStackFilter() || ctx.Options.GetSoftDeleteMode() != SoftDeleteQueryMode.ActiveOnly))
                return;

            _logger.LogTrace("Source: {Filter} Stack Filter: {StackFilter} Inverted Stack Filter: {InvertedStackFilter}", filter, stackFilter.Filter, stackFilter.InvertedFilter);

            if (!(ctx is IQueryVisitorContextWithValidator)) {
                var systemFilterQuery = GetSystemFilterQuery(ctx, isStackIdsNegated);
                systemFilterQuery.FilterExpression(stackFilterValue);
                var softDeleteMode = isStackIdsNegated ? SoftDeleteQueryMode.All : SoftDeleteQueryMode.ActiveOnly;
                systemFilterQuery.EventStackFilterInverted(isStackIdsNegated);

                FindResults<Stack> results = null;
                var tooManyStacksCheck = await _cacheClient.GetAsync<long>(GetQueryHash(systemFilterQuery));
                if (tooManyStacksCheck.HasValue) {
                    stackTotal = tooManyStacksCheck.Value;
                } else {
                    results = await _stackRepository.GetIdsByQueryAsync(q => systemFilterQuery.As<Stack>(), o => o.PageLimit(stackIdLimit).SoftDeleteMode(softDeleteMode)).AnyContext();
                    stackTotal = results.Total;
                }
                
                if (stackTotal > stackIdLimit) {
                    if (!tooManyStacksCheck.HasValue)
                        await _cacheClient.SetAsync(GetQueryHash(systemFilterQuery), stackTotal, TimeSpan.FromMinutes(15));

                    _logger.LogTrace("Query: {query} will be inverted due to id limit: {ResultCount}", stackFilterValue, stackTotal);
                    isStackIdsNegated = !isStackIdsNegated;
                    stackFilterValue = isStackIdsNegated ? stackFilter.InvertedFilter : stackFilter.Filter;
                    systemFilterQuery.FilterExpression(stackFilterValue);
                    softDeleteMode = isStackIdsNegated ? SoftDeleteQueryMode.All : SoftDeleteQueryMode.ActiveOnly;
                    systemFilterQuery.EventStackFilterInverted(isStackIdsNegated);
                    
                    tooManyStacksCheck = await _cacheClient.GetAsync<long>(GetQueryHash(systemFilterQuery));
                    if (tooManyStacksCheck.HasValue) {
                        stackTotal = tooManyStacksCheck.Value;
                    } else {
                        results = await _stackRepository.GetIdsByQueryAsync(q => systemFilterQuery.As<Stack>(), o => o.PageLimit(stackIdLimit).SoftDeleteMode(softDeleteMode)).AnyContext();
                        stackTotal = results.Total;
                    }
                }

                if (stackTotal > stackIdLimit) {
                    if (!tooManyStacksCheck.HasValue)
                        await _cacheClient.SetAsync(GetQueryHash(systemFilterQuery), stackTotal, TimeSpan.FromMinutes(15));
                    throw new DocumentLimitExceededException("Please limit your search criteria.");
                }

                if (results?.Hits != null)
                    stackIds = results.Hits.Select(h => h.Id).ToArray();
            }

            _logger.LogTrace("Setting stack filter with {IdCount} ids", stackIds?.Length ?? 0);

            if (!isStackIdsNegated) {
                if (stackIds.Length > 0)
                    ctx.Source.Stack(stackIds);
                else
                    ctx.Source.Stack("none");
            } else {
                if (stackIds.Length > 0)
                    ctx.Source.ExcludeStack(stackIds);
            }

            // Strips stack only fields and stack only special fields
            string eventFilter = await _eventStackFilter.GetEventFilterAsync(filter, ctx);
            ctx.Source.FilterExpression(eventFilter);
        }

        private IRepositoryQuery GetSystemFilterQuery(IQueryVisitorContext context, bool isStackIdsNegated) {
            var builderContext = context as IQueryBuilderContext;
            var systemFilter = builderContext?.Source.GetSystemFilter();
            var systemFilterQuery = systemFilter?.GetQuery().Clone();
            if (systemFilterQuery == null) {
                systemFilterQuery = new RepositoryQuery<Stack>();
                foreach (var range in builderContext?.Source.GetDateRanges() ?? Enumerable.Empty<DateRange>()) {
                    systemFilterQuery.DateRange(range.StartDate, range.EndDate, range.Field, range.TimeZone);
                }
            }

            if (!systemFilterQuery.HasAppFilter())
                systemFilterQuery.AppFilter(builderContext?.Source.GetAppFilter());

            foreach (var range in systemFilterQuery.GetDateRanges()) {
                if (range.Field == _inferredEventDateField || range.Field == "date") {
                    range.Field = _inferredStackLastOccurrenceField;
                    if (isStackIdsNegated) // don't apply retention date filter on inverted stack queries
                        range.StartDate = null;
                    range.EndDate = null;
                }
            }

            return systemFilterQuery;
        }

        private string GetQueryHash(IRepositoryQuery query) {
            // org ids, project ids, date ranges, filter expression

            var appFilter = query.GetAppFilter();
            var projectIds = query.GetProjects();
            if (projectIds.Count == 0 && appFilter?.Projects != null)
                projectIds.AddRange(appFilter.Projects.Select(p => p.Id));
            var organizationIds = query.GetOrganizations();
            if (organizationIds.Count == 0 && appFilter?.Organizations != null)
                organizationIds.AddRange(appFilter.Organizations.Select(o => o.Id));

            var hashCode = new HashCode();
            
            if (projectIds.Count > 0)
                foreach (string projectId in projectIds)
                    hashCode.Add(projectId);
            else if (organizationIds.Count > 0)
                foreach (string organizationId in organizationIds)
                    hashCode.Add(organizationId);

            var dateRanges = query.GetDateRanges();
            var minDate = dateRanges.Min(r => r.StartDate) ?? DateTime.MinValue;
            var maxDate = dateRanges.Max(r => r.EndDate) ?? DateTime.MaxValue;
            
            hashCode.Add(minDate);
            hashCode.Add(maxDate);
            
            hashCode.Add(query.GetFilterExpression());

            string cacheScope = "";
            if (organizationIds.Count == 1 && projectIds.Count == 1)
                cacheScope = String.Concat(organizationIds.Single(), ":", projectIds.Single(), ":");
            else if (organizationIds.Count == 1)
                cacheScope = String.Concat(organizationIds.Single(), ":");
            else if (projectIds.Count == 1)
                cacheScope = String.Concat("project:", projectIds.Single(), ":");

            return String.Concat(cacheScope, hashCode.ToHashCode().ToString());
        }
    }
}
