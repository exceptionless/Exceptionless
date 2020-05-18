using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Options;
using Foundatio.Parsers.ElasticQueries;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Options;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Nest;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Core.Repositories.Queries {
    public class EventJoinFilterVisitor : ChainableQueryVisitor {
        private readonly IStackRepository _stackRepository;
        public const string StackFieldName = "@stack";
        private static readonly HashSet<string> _fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            StackIndex.Alias.FirstOccurrence,
            "first_occurrence",
            StackIndex.Alias.LastOccurrence,
            "last_occurrence",
            "references",
            StackIndex.Alias.References,
            "status",
            "snooze_until_utc",
            "signature_hash",
            StackIndex.Alias.SignatureHash,
            StackIndex.Alias.OccurrencesAreCritical,
            "title",
            "description",
            "occurrences_are_critical",
            StackIndex.Alias.DateFixed,
            "date_fixed",
            StackIndex.Alias.FixedInVersion,
            "fixed_in_version",
            StackIndex.Alias.OccurrencesAreCritical,
            "occurrences_are_critical",
            StackIndex.Alias.TotalOccurrences,
            "total_occurrences",
        };
        
        // Fields to migrate: 
        private static readonly HashSet<string> _fieldsToMigrate = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            StackIndex.Alias.IsFixed,
            "is_fixed",
            StackIndex.Alias.IsHidden,
            "is_hidden",
            StackIndex.Alias.IsRegressed,
            "is_regressed"
        };

        // TODO: will it be goofy with aggregations.
        
        private readonly ILogger _logger;

        public EventJoinFilterVisitor(IStackRepository stackRepository, ILoggerFactory loggerFactory) {
            _stackRepository = stackRepository;
            _logger = loggerFactory.CreateLogger<EventJoinFilterVisitor>();
        }

        public override async Task VisitAsync(GroupNode node, IQueryVisitorContext context) {
            bool isTraceLogLevelEnabled = _logger.IsEnabled(LogLevel.Trace);
            if (isTraceLogLevelEnabled) 
                _logger.LogTrace("Visiting GroupNode: {GroupNode}", node);
            
            if (node.Field == StackFieldName && node.Left != null) {
                string term = ToTerm(node);
                if (isTraceLogLevelEnabled)
                    _logger.LogTrace("Visiting GroupNode Field {FieldName} with resolved term: {Term}", node.Field, term);
                
                var builderContext = context as IQueryBuilderContext;
                var systemFilter = builderContext?.Source.GetAppFilter();
                var ranges = builderContext?.Source.GetDateRanges();
                var utcStart = ranges?.Where(r => r.UseStartDate).OrderBy(r => r.StartDate).FirstOrDefault(r => r.Field == "date")?.StartDate ?? DateTime.MinValue;
                var utcEnd = ranges?.Where(r => r.UseEndDate).OrderByDescending(r => r.EndDate).FirstOrDefault(r => r.Field == "date")?.StartDate ?? DateTime.MaxValue;
                var stackIds = await _stackRepository.GetIdsByFilterAsync(systemFilter, term, utcStart, utcEnd, o => o.PageLimit(10000)).AnyContext();
                if (isTraceLogLevelEnabled) 
                    _logger.LogTrace("Setting term query with {IdCount} ids on parent GroupNode: {GroupNode}", stackIds?.Length ?? 0, node.Parent);

                var parentQuery = node.Parent.GetQuery();
                var nodeOperator = node.Parent is GroupNode gn ? gn.Operator : node.Operator;
                var query = new TermsQuery { Field = "stack_id", Terms = stackIds != null && stackIds.Length > 0 ? stackIds : new[] { "none" } };
                bool isNegated = node.IsNegated.GetValueOrDefault() || node.Prefix == "-"; // TODO: We need to get parsers to populate the isNegated.
                if (nodeOperator == GroupOperator.Or)
                    node.Parent.SetQuery(parentQuery || (isNegated ? !query : query));
                else
                    node.Parent.SetQuery(parentQuery && (isNegated ? !query : query));
                
                node.Left = null;
                node.Right = null;
                if (isTraceLogLevelEnabled) 
                    _logger.LogTrace("Reset left and right node for: {GroupNode}", node);
            } 
            
            await base.VisitAsync(node, context).AnyContext();
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