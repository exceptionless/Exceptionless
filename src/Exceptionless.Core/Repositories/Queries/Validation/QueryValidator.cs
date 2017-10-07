using System;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Foundatio.Parsers.LuceneQueries;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Exceptionless.Core.Queries.Validation {
    public interface IQueryValidator {
        Task<QueryValidator.QueryProcessResult> ValidateQueryAsync(string query);

        Task<QueryValidator.QueryProcessResult> ValidateQueryAsync(IQueryNode query);

        Task<QueryValidator.QueryProcessResult> ValidateAggregationsAsync(string aggs);

        Task<QueryValidator.QueryProcessResult> ValidateAggregationsAsync(IQueryNode query);
    }

    public class QueryValidator : IQueryValidator {
        private readonly IQueryParser _parser;
        private readonly ILogger _logger;

        public QueryValidator(IQueryParser parser, ILoggerFactory loggerFactory) {
            _parser = parser;
            _logger = loggerFactory?.CreateLogger(GetType());
        }

        public async Task<QueryProcessResult> ValidateQueryAsync(string query) {
            if (String.IsNullOrWhiteSpace(query))
                return new QueryProcessResult { IsValid = true };

            IQueryNode parsedResult;
            try {
                parsedResult = await _parser.ParseAsync(query, QueryType.Query).AnyContext();
            } catch (Exception ex) {
                _logger.LogError(ex, "Error parsing query: {Query}", query);
                return new QueryProcessResult { Message = ex.Message };
            }

            return await ValidateQueryAsync(parsedResult).AnyContext();
        }

        public async Task<QueryProcessResult> ValidateQueryAsync(IQueryNode query) {
            var info = await ValidationVisitor.RunAsync(query).AnyContext();
            return ApplyQueryRules(info);
        }

        protected virtual QueryProcessResult ApplyQueryRules(QueryValidationInfo info) {
            return new QueryProcessResult { IsValid = info.IsValid };
        }

        public async Task<QueryProcessResult> ValidateAggregationsAsync(string aggs) {
            if (String.IsNullOrWhiteSpace(aggs))
                return new QueryProcessResult { IsValid = true };

            IQueryNode parsedResult;
            try {
                parsedResult = await _parser.ParseAsync(aggs, QueryType.Aggregation).AnyContext();
            } catch (Exception ex) {
                _logger.LogError(ex, "Error parsing aggregation: {Aggregation}", aggs);
                return new QueryProcessResult { Message = ex.Message };
            }

            return await ValidateAggregationsAsync(parsedResult).AnyContext();
        }

        public async Task<QueryProcessResult> ValidateAggregationsAsync(IQueryNode query) {
            var info = await ValidationVisitor.RunAsync(query).AnyContext();
            return ApplyAggregationRules(info);
        }

        protected virtual QueryProcessResult ApplyAggregationRules(QueryValidationInfo info) {
            return new QueryProcessResult { IsValid = info.IsValid };
        }

        public class QueryProcessResult {
            public bool IsValid { get; set; }
            public string Message { get; set; }
            public bool UsesPremiumFeatures { get; set; }
        }
    }
}