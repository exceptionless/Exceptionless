using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Queries.Validation {
    public sealed class StackQueryValidator : QueryValidator {
        private readonly HashSet<string> _freeQueryFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            StackIndex.Alias.FirstOccurrence,
            "first_occurrence",
            StackIndex.Alias.LastOccurrence,
            "last_occurrence",
            StackIndex.Alias.IsFixed,
            StackIndex.Alias.IsHidden,
            "is_hidden",
            StackIndex.Alias.IsRegressed,
            "is_regressed",
            StackIndex.Alias.Type,
            StackIndex.Alias.OccurrencesAreCritical,
            "occurrences_are_critical",
            StackIndex.Alias.OrganizationId,
            "organization_id",
            StackIndex.Alias.ProjectId,
            "project_id"
        };

        private static readonly HashSet<string> _freeAggregationFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            StackIndex.Alias.FirstOccurrence,
            "first_occurrence",
            StackIndex.Alias.LastOccurrence,
            "last_occurrence",
            StackIndex.Alias.IsFixed,
            StackIndex.Alias.IsHidden,
            "is_hidden",
            StackIndex.Alias.OccurrencesAreCritical,
            "occurrences_are_critical",
            StackIndex.Alias.IsRegressed,
            "is_regressed",
            StackIndex.Alias.Type
        };

        private static readonly HashSet<string> _allowedAggregationFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            StackIndex.Alias.FirstOccurrence,
            "first_occurrence",
            StackIndex.Alias.LastOccurrence,
            "last_occurrence",
            StackIndex.Alias.IsFixed,
            StackIndex.Alias.DateFixed,
            "date_fixed",
            StackIndex.Alias.FixedInVersion,
            "fixed_in_version",
            StackIndex.Alias.IsHidden,
            "is_hidden",
            StackIndex.Alias.OccurrencesAreCritical,
            "occurrences_are_critical",
            StackIndex.Alias.TotalOccurrences,
            "total_occurrences",
            StackIndex.Alias.IsRegressed,
            "is_regressed",
            StackIndex.Alias.Type,
            StackIndex.Alias.OrganizationId,
            "organization_id",
            StackIndex.Alias.ProjectId,
            "project_id"
        };

        public StackQueryValidator(ExceptionlessElasticConfiguration configuration, ILoggerFactory loggerFactory) : base(configuration.Stacks.QueryParser, loggerFactory) { }

        protected override QueryProcessResult ApplyQueryRules(QueryValidationInfo info) {
            return new QueryProcessResult {
                IsValid = info.IsValid,
                UsesPremiumFeatures = !info.ReferencedFields.All(_freeQueryFields.Contains)
            };
        }

        protected override QueryProcessResult ApplyAggregationRules(QueryValidationInfo info) {
            if (!info.IsValid)
                return new QueryProcessResult { Message = "Invalid aggregation" };

            if (info.MaxNodeDepth > 6)
                return new QueryProcessResult { Message = "Aggregation max depth exceeded" };

            if (info.Operations.Values.Sum(o => o.Count) > 10)
                return new QueryProcessResult { Message = "Aggregation count exceeded" };

            // Only allow fields that are numeric or have high commonality.
            if (!info.ReferencedFields.All(_allowedAggregationFields.Contains))
                return new QueryProcessResult { Message = "One or more aggregation fields are not allowed" };

            // Distinct queries are expensive.
            if (info.Operations.TryGetValue(AggregationType.Cardinality, out var values) && values.Count > 3)
                return new QueryProcessResult { Message = "Cardinality aggregation count exceeded" };

            // Term queries are expensive.
            if (info.Operations.TryGetValue(AggregationType.Terms, out values) && values.Count > 3)
                return new QueryProcessResult { Message = "Terms aggregation count exceeded" };

            bool usesPremiumFeatures = !info.ReferencedFields.All(_freeAggregationFields.Contains);
            return new QueryProcessResult {
                IsValid = info.IsValid,
                UsesPremiumFeatures = usesPremiumFeatures
            };
        }
    }
}