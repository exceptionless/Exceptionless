using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Extensions;
using Foundatio.Parsers.ElasticQueries.Visitors;
using Foundatio.Parsers.LuceneQueries;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;
using System.Threading.Tasks;

namespace Exceptionless.Core.Processors {
    public class QueryProcessor {
        private static readonly HashSet<string> _freeFields = new HashSet<string> {
            "hidden",
            "fixed",
            "type",
            "reference",
            "organization",
            "project",
            "stack"
        };

        public static async Task<QueryProcessResult> ProcessAsync(string query) {
            if (String.IsNullOrWhiteSpace(query))
                return new QueryProcessResult { IsValid = true };

            GroupNode result;
            try {
                var parser = new LuceneQueryParser();
                result = parser.Parse(query);
            } catch (Exception ex) {
                return new QueryProcessResult { Message = ex.Message };
            }

            var validator = new QueryProcessorVisitor(_freeFields);
            var context = new ElasticQueryVisitorContext();
            await result.AcceptAsync(validator, context).AnyContext();

            var expandedQuery = validator.UsesDataFields ? GenerateQueryVisitor.Run(result) : query;
            return new QueryProcessResult {
                IsValid = true,
                UsesPremiumFeatures = validator.UsesPremiumFeatures,
                ExpandedQuery = expandedQuery
            };
        }

        public static async Task<QueryProcessResult> ValidateAsync(string query) {
            if (String.IsNullOrEmpty(query))
                return new QueryProcessResult { IsValid = true };

            GroupNode result;
            try {
                var parser = new LuceneQueryParser();
                result = parser.Parse(query);
            } catch (Exception ex) {
                return new QueryProcessResult { Message = ex.Message };
            }

            var validator = new QueryProcessorVisitor(_freeFields);
            var context = new ElasticQueryVisitorContext();
            await result.AcceptAsync(validator, context).AnyContext();

            return new QueryProcessResult {
                IsValid = true,
                UsesPremiumFeatures = validator.UsesPremiumFeatures
            };
        }
    }

    public class QueryProcessorVisitor : IQueryNodeVisitor {
        private readonly HashSet<string> _freeFields;

        public QueryProcessorVisitor(HashSet<string> freeFields) {
            _freeFields = freeFields ?? new HashSet<string>();
        }

        public async Task VisitAsync(GroupNode node, IQueryVisitorContext context) {
            var childTerms = new List<string>();
            var leftTermNode = node.Left as TermNode;
            if (leftTermNode != null && leftTermNode.Field == null)
                childTerms.Add(leftTermNode.Term);

            var leftTermRangeNode = node.Left as TermRangeNode;
            if (leftTermRangeNode != null && leftTermRangeNode.Field == null) {
                childTerms.Add(leftTermRangeNode.Min);
                childTerms.Add(leftTermRangeNode.Max);
            }

            var rightTermNode = node.Right as TermNode;
            if (rightTermNode != null && rightTermNode.Field == null)
                childTerms.Add(rightTermNode.Term);

            var rightTermRangeNode = node.Right as TermRangeNode;
            if (rightTermRangeNode != null && rightTermRangeNode.Field == null) {
                childTerms.Add(rightTermRangeNode.Min);
                childTerms.Add(rightTermRangeNode.Max);
            }

            node.Field = GetCustomFieldName(node.Field, childTerms.ToArray()) ?? node.Field;
            foreach (var child in node.Children)
                await child.AcceptAsync(this, context).AnyContext();
        }

        public Task VisitAsync(TermNode node, IQueryVisitorContext context) {
            // using all fields search
            if (String.IsNullOrEmpty(node.Field)) {
                UsesPremiumFeatures = true;
                return Task.CompletedTask;
            }

            node.Field = GetCustomFieldName(node.Field, node.Term) ?? node.Field;
            return Task.CompletedTask;
        }

        public Task VisitAsync(TermRangeNode node, IQueryVisitorContext context) {
            node.Field = GetCustomFieldName(node.Field, node.Min, node.Max) ?? node.Field;
            return Task.CompletedTask;
        }

        public Task VisitAsync(ExistsNode node, IQueryVisitorContext context) {
            node.Field = GetCustomFieldName(node.Field) ?? node.Field;
            return Task.CompletedTask;
        }

        public Task VisitAsync(MissingNode node, IQueryVisitorContext context) {
            node.Field = GetCustomFieldName(node.Field) ?? node.Field;
            return Task.CompletedTask;
        }

        private string GetCustomFieldName(string field, params string[] terms) {
            if (String.IsNullOrEmpty(field))
                return null;

            // using a field not in the free list
            if (!_freeFields.Contains(field))
                UsesPremiumFeatures = true;

            if (field.StartsWith("data.")) {
                UsesDataFields = true;
                string termType = GetTermType(terms);
                return $"idx.{field.Substring(5).ToLower()}-{termType}";
            }

            if (field.StartsWith("ref.")) {
                UsesDataFields = true;
                return $"idx.{field.Substring(4).ToLower()}-r";
            }

            return null;
        }

        private static string GetTermType(params string[] terms) {
            string termType = "s";

            var trimmedTerms = terms.Where(t => t != null).Distinct().ToList();
            foreach (var term in trimmedTerms) {
                if (term.StartsWith("*"))
                    continue;

                bool boolResult;
                DateTime dateResult;
                if (Boolean.TryParse(term, out boolResult))
                    termType = "b";
                else if (term.IsNumeric())
                    termType = "n";
                else if (DateTime.TryParse(term, out dateResult))
                    termType = "d";

                break;
            }

            // Some terms can be a string date range: [now TO now/d+1d}
            if (String.Equals(termType, "s") && trimmedTerms.All(t => String.Equals(t, "now", StringComparison.OrdinalIgnoreCase) || t.StartsWith("now/", StringComparison.OrdinalIgnoreCase)))
                termType = "d";

            return termType;
        }

        public bool UsesPremiumFeatures { get; set; }
        public bool UsesDataFields { get; set; }
    }

    public class QueryProcessResult {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public string ExpandedQuery { get; set; }
        public bool UsesPremiumFeatures { get; set; }
    }
}
