using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Core.Repositories.Options;
using Foundatio.Parsers.LuceneQueries.Extensions;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Options;
using Microsoft.Extensions.Logging;
using Nest;
using DateRange = Foundatio.Repositories.DateRange;

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

        public EventStackFilterQueryBuilder(IStackRepository stackRepository, ILoggerFactory loggerFactory) {
            _stackRepository = stackRepository;
            _logger = loggerFactory.CreateLogger<EventStackFilterQueryBuilder>();
            _inferredEventDateField = Infer.Field<PersistentEvent>(f => f.Date);
            _inferredStackLastOccurrenceField = Infer.Field<Stack>(f => f.LastOccurrence);
        }

        public async Task BuildAsync<T>(QueryBuilderContext<T> ctx) where T : class, new() {
            string filter = ctx.Source.GetFilterExpression() ?? String.Empty;

            if (String.IsNullOrEmpty(filter) && !ctx.Source.ShouldEnforceEventStackFilter())
                return;

            // TODO: Handle search expressions as well
            bool altInvertRequested = false;
            if (filter.StartsWith("@!")) {
                altInvertRequested = true;
                filter = filter.Substring(2);
                ctx.Source.FilterExpression(filter);
            }

            var stackFilter = await EventStackFilterQueryVisitor.RunAsync(filter, EventStackFilterQueryMode.Stacks, ctx);
            var invertedStackFilter = await EventStackFilterQueryVisitor.RunAsync(filter, EventStackFilterQueryMode.InvertedStacks, ctx);

            if (ctx.Options.GetSoftDeleteMode() == SoftDeleteQueryMode.ActiveOnly)
                invertedStackFilter.Query = !String.IsNullOrEmpty(invertedStackFilter.Query) ? $"(is_deleted:true OR ({invertedStackFilter.Query}))" : "is_deleted:true";

            // queries are the same, no need to allow inverting
            if (invertedStackFilter.Query == stackFilter.Query)
                invertedStackFilter.IsInvertSuccessful = false;

            const int stackIdLimit = 10000;
            string[] stackIds = null;

            string query = stackFilter.Query;
            bool isStackIdsNegated = stackFilter.HasStatusOpen && invertedStackFilter.IsInvertSuccessful && !altInvertRequested;
            if (isStackIdsNegated)
                query = invertedStackFilter.Query;

            if (String.IsNullOrEmpty(query) && (!ctx.Source.ShouldEnforceEventStackFilter() || ctx.Options.GetSoftDeleteMode() != SoftDeleteQueryMode.ActiveOnly))
                return;

            _logger.LogTrace("Stack filter: {StackFilter} Invert Success: {InvertSuccess} Inverted: {InvertedStackFilter}", stackFilter.Query, invertedStackFilter.IsInvertSuccessful, invertedStackFilter.Query);

            if (!(ctx is IQueryVisitorContextWithValidator)) {
                var systemFilterQuery = GetSystemFilterQuery(ctx, isStackIdsNegated);
                systemFilterQuery.FilterExpression(query);
                var softDeleteMode = isStackIdsNegated ? SoftDeleteQueryMode.All : SoftDeleteQueryMode.ActiveOnly;
                systemFilterQuery.EventStackFilterInverted(isStackIdsNegated);
                var results = await _stackRepository.GetIdsByQueryAsync(q => systemFilterQuery.As<Stack>(), o => o.PageLimit(stackIdLimit).SoftDeleteMode(softDeleteMode)).AnyContext();
                if (results.Total > stackIdLimit && (isStackIdsNegated || invertedStackFilter.IsInvertSuccessful)) {
                    isStackIdsNegated = !isStackIdsNegated;
                    query = isStackIdsNegated ? invertedStackFilter.Query : stackFilter.Query;
                    systemFilterQuery.FilterExpression(query);
                    softDeleteMode = isStackIdsNegated ? SoftDeleteQueryMode.All : SoftDeleteQueryMode.ActiveOnly;
                    systemFilterQuery.EventStackFilterInverted(isStackIdsNegated);
                    results = await _stackRepository.GetIdsByQueryAsync(q => systemFilterQuery.As<Stack>(), o => o.PageLimit(stackIdLimit).SoftDeleteMode(softDeleteMode)).AnyContext();
                }

                if (results.Total > stackIdLimit)
                    throw new DocumentLimitExceededException("Please limit your search criteria.");

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

            var eventsResult = await EventStackFilterQueryVisitor.RunAsync(filter, EventStackFilterQueryMode.Events, ctx);
            ctx.Source.FilterExpression(eventsResult.Query);
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

            foreach (var range in systemFilterQuery.GetDateRanges() ?? Enumerable.Empty<DateRange>()) {
                if (range.Field == _inferredEventDateField || range.Field == "date") {
                    range.Field = _inferredStackLastOccurrenceField;
                    if (isStackIdsNegated) // don't apply retention date filter on inverted stack queries
                        range.StartDate = null;
                    range.EndDate = null;
                }
            }

            return systemFilterQuery;
        }
    }
}
