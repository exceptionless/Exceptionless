using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Core.Repositories.Options;
using Foundatio.Parsers.ElasticQueries;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Options;
using Microsoft.Extensions.Logging;
using Nest;
using DateRange = Foundatio.Repositories.DateRange;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Core.Repositories.Queries {
    public class EventJoinFilterVisitor : ChainableQueryVisitor {
        public const string StackFieldName = "@stack";
        private readonly IStackRepository _stackRepository;
        private readonly ILogger _logger;
        private readonly Field _inferredEventDateField;
        private readonly Field _inferredStackLastOccurrenceField;

        public EventJoinFilterVisitor(IStackRepository stackRepository, ILoggerFactory loggerFactory) {
            _stackRepository = stackRepository;
            _logger = loggerFactory.CreateLogger<EventJoinFilterVisitor>();
            _inferredEventDateField = Infer.Field<PersistentEvent>(f => f.Date);
            _inferredStackLastOccurrenceField = Infer.Field<Stack>(f => f.LastOccurrence);
        }

        public override async Task VisitAsync(GroupNode node, IQueryVisitorContext context) {
            bool isTraceLogLevelEnabled = _logger.IsEnabled(LogLevel.Trace);
            if (isTraceLogLevelEnabled) 
                _logger.LogTrace("Visiting GroupNode: {GroupNode}", node);
            
            if (node.Field == StackFieldName && node.Left != null) {
                string term = ToTerm(node);
                if (isTraceLogLevelEnabled)
                    _logger.LogTrace("Visiting GroupNode Field {FieldName} with resolved term: {Term}", node.Field, term);

                const int stackIdLimit = 10000;
                string[] stackIds = null; 
                bool isStackIdsNegated = await HasStatusOpenVisitor.RunAsync(node).AnyContext();
                if (!(context is IQueryVisitorContextWithValidator)) {
                    var systemFilterQuery = GetSystemFilterQuery(context);
                    systemFilterQuery.FilterExpression(isStackIdsNegated ? $"-({term}) OR deleted:true" : term);
                    var softDeleteMode = isStackIdsNegated ? SoftDeleteQueryMode.All : SoftDeleteQueryMode.ActiveOnly;
                    var results = await _stackRepository.GetIdsByQueryAsync(q => systemFilterQuery.As<Stack>(), o => o.PageLimit(stackIdLimit).SoftDeleteMode(softDeleteMode)).AnyContext();
                    if (results.Total > stackIdLimit) {
                        isStackIdsNegated = !isStackIdsNegated;
                        systemFilterQuery.FilterExpression(isStackIdsNegated ? $"-({term}) OR deleted:true" : term);
                        softDeleteMode = isStackIdsNegated ? SoftDeleteQueryMode.All : SoftDeleteQueryMode.ActiveOnly;
                        results = await _stackRepository.GetIdsByQueryAsync(q => systemFilterQuery.As<Stack>(), o => o.PageLimit(stackIdLimit).SoftDeleteMode(softDeleteMode)).AnyContext();
                    }
                    
                    if (results.Total > stackIdLimit)
                        throw new DocumentLimitExceededException("Please limit your search criteria.");
                    
                    stackIds = results.Hits.Select(h => h.Id).ToArray();
                }
                
                if (isTraceLogLevelEnabled) 
                    _logger.LogTrace("Setting term query with {IdCount} ids on parent GroupNode: {GroupNode}", stackIds?.Length ?? 0, node.Parent);

                var parentQuery = node.Parent?.GetQuery();
                var parentNode = node.Parent ?? node;
                var nodeOperator = parentNode is GroupNode gn ? gn.Operator : node.Operator;
                var query = new TermsQuery { Field = "stack_id", Terms = stackIds != null && stackIds.Length > 0 ? stackIds : new[] { "none" } };
                
                bool isNegated = node.IsNegated.GetValueOrDefault() || node.Prefix == "-"; // TODO: We need to get parsers to populate the isNegated.
                if (isStackIdsNegated)
                    isNegated = !isNegated;
                    
                if (nodeOperator == GroupOperator.Or)
                    parentNode.SetQuery(parentQuery || (isNegated ? !query : query));
                else
                    parentNode.SetQuery(parentQuery && (isNegated ? !query : query));
                
                node.Left = null;
                node.Right = null;
                if (isTraceLogLevelEnabled) 
                    _logger.LogTrace("Reset left and right node for: {GroupNode}", node);
            } 
            
            await base.VisitAsync(node, context).AnyContext();
        }

        private IRepositoryQuery GetSystemFilterQuery(IQueryVisitorContext context){
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
                if (range.Field == _inferredEventDateField || range.Field == "date")
                    range.Field = _inferredStackLastOccurrenceField;
            }

            return systemFilterQuery;
        }

        public static Task<IQueryNode> RunAsync(IQueryNode node, EventJoinFilterVisitor visitor, IQueryVisitorContextWithIncludeResolver context = null) {
            return visitor.AcceptAsync(node, context ?? new QueryVisitorContextWithIncludeResolver());
        }

        public static IQueryNode Run(IQueryNode node, EventJoinFilterVisitor visitor, IQueryVisitorContextWithIncludeResolver context = null) {
            return RunAsync(node, visitor, context).GetAwaiter().GetResult();
        }
        
        private string ToTerm(GroupNode node) {
            var builder = new StringBuilder();

            if (node.Left != null)
                builder.Append(node.Left);
            
            if (node.Operator == GroupOperator.And)
                builder.Append(" AND ");
            else if (node.Operator == GroupOperator.Or)
                builder.Append(" OR ");
            else if (node.Right != null)
                builder.Append(" ");
            
            if (node.Right != null) 
                builder.Append(node.Right);

            return builder.ToString();
        }
    }

    public static class UseEventJoinFilterVisitorExtensions {
        public static ElasticQueryParserConfiguration UseEventJoinFilterVisitor(this ElasticQueryParserConfiguration configuration, EventJoinFilterVisitor visitor) {
            return configuration.AddVisitorAfter<IncludeVisitor>(visitor);
        }
    }
}