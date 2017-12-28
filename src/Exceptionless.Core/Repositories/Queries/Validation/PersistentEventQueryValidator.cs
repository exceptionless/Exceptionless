using System;
using System.Collections.Generic;
using System.Linq;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Parsers.ElasticQueries.Visitors;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Queries.Validation {
    public sealed class PersistentEventQueryValidator : QueryValidator {
        private readonly HashSet<string> _freeQueryFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "date",
            "is_hidden",
            "is_fixed",
            "type",
            "reference_id",
            "organization_id",
            "project_id",
            "stack_id"
        };

        private static readonly HashSet<string> _freeAggregationFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "date",
            "type",
            "value",
            "count",
            "is_first_occurrence",
            "stack_id",
            "data.@user.identity"
        };

        private static readonly HashSet<string> _allowedAggregationFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "date",
            "source",
            "tags",
            "type",
            "value",
            "count",
            "geo",
            "is_fixed",
            "is_hidden",
            "is_first_occurrence",
            "organization_id",
            "project_id",
            "stack_id",
            "os",
            "error.code",
            "error.type",
            "error.targettype",
            "error.targetmethod",
            "data.@environment.machine_name",
            "data.@environment.architecture",
            "data.@location.country",
            "data.@location.level1",
            "data.@location.level2",
            "data.@location.locality",
            "data.@request.data.@browser",
            "data.@request.data.@browser_major_version",
            "data.@request.data.@device",
            "data.@request.data.@os_version",
            "data.@request.data.@os_major_version",
            "data.@request.data.@is_bot",
            "data.@version",
            "data.@user.identity"
        };

        public PersistentEventQueryValidator(ExceptionlessElasticConfiguration configuration, ILoggerFactory loggerFactory) : base(configuration.Events.Event.QueryParser, loggerFactory) {}

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
            if (info.Operations.TryGetValue(AggregationType.Cardinality, out ICollection<string> values) && values.Count > 3)
                return new QueryProcessResult { Message = "Cardinality aggregation count exceeded" };

            // Term queries are expensive.
            if (info.Operations.TryGetValue(AggregationType.Terms, out values) && (values.Count > 3))
                return new QueryProcessResult { Message = "Terms aggregation count exceeded" };

            bool usesPremiumFeatures = !info.ReferencedFields.All(_freeAggregationFields.Contains);
            return new QueryProcessResult {
                IsValid = info.IsValid,
                UsesPremiumFeatures = usesPremiumFeatures
            };
        }
    }
}