using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.LuceneQueryParser;
using Exceptionless.LuceneQueryParser.Nodes;
using Exceptionless.LuceneQueryParser.Visitor;

namespace Exceptionless.Core.Filter {
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

        public static QueryProcessResult Process(string query) {
            if (String.IsNullOrEmpty(query))
                return new QueryProcessResult { IsValid = true };

            GroupNode result;
            try {
                var parser = new QueryParser();
                result = parser.Parse(query);
            } catch (Exception ex) {
                return new QueryProcessResult { Message = ex.Message };
            }

            var validator = new QueryProcessorVisitor(_freeFields);
            result.Accept(validator);

            string expandedQuery = validator.UsesDataFields ? GenerateQueryVisitor.Run(result) : query;

            return new QueryProcessResult { IsValid = true, UsesPremiumFeatures = validator.UsesPremiumFeatures, ExpandedQuery = expandedQuery };
        }

        public static QueryProcessResult Validate(string query) {
            if (String.IsNullOrEmpty(query))
                return new QueryProcessResult { IsValid = true };

            GroupNode result;
            try {
                var parser = new QueryParser();
                result = parser.Parse(query);
            } catch (Exception ex) {
                return new QueryProcessResult { Message = ex.Message };
            }

            var validator = new QueryProcessorVisitor(_freeFields);
            result.Accept(validator);

            return new QueryProcessResult { IsValid = true, UsesPremiumFeatures = validator.UsesPremiumFeatures };
        }
    }

    public class QueryProcessorVisitor : QueryNodeVisitorBase {
        private readonly HashSet<string> _freeFields;

        public QueryProcessorVisitor(HashSet<string> freeFields) {
            _freeFields = freeFields ?? new HashSet<string>();
        }

        public override void Visit(GroupNode node) {
            if (node.Field != null) {
                // using a field not in the free list
                if (!_freeFields.Contains(node.Field.Field))
                    UsesPremiumFeatures = true;

                if (node.Field.Field.StartsWith("data.")) {
                    UsesDataFields = true;

                    var lt = node.Left as TermNode;
                    var rt = node.Right as TermNode;
                    string termType = GetTermType(lt?.TermMin, lt?.TermMax, lt?.Term, rt?.TermMin, rt?.TermMax, rt?.Term);
                    node.Field.Field = $"idx.{node.Field.Field.ToLower().Substring(5)}-{termType}";
                } else if (node.Field.Field.StartsWith("ref.")) {
                    UsesDataFields = true;
                    node.Field.Field = $"idx.{node.Field.Field.ToLower().Substring(4)}-r";
                }
            }

            base.Visit(node);
        }

        public override void Visit(TermNode node) {
            // using all fields search
            if (String.IsNullOrEmpty(node.Field?.Field))
                UsesPremiumFeatures = true;

            if (node.Field != null) {
                // using a field not in the free list
                if (!_freeFields.Contains(node.Field.Field))
                    UsesPremiumFeatures = true;

                if (node.Field.Field.StartsWith("data.")) {
                    UsesDataFields = true;
                    string termType = GetTermType(node.TermMin, node.TermMax, node.Term);
                    node.Field.Field = $"idx.{node.Field.Field.ToLower().Substring(5)}-{termType}";
                } else if (node.Field.Field.StartsWith("ref.")) {
                    UsesDataFields = true;
                    node.Field.Field = $"idx.{node.Field.Field.ToLower().Substring(4)}-r";
                }
            }

            base.Visit(node);
        }

        private static string GetTermType(params string[] terms) {
            string termType = "s";

            var trimmedTerms = terms.Where(t => t != null).Select(t => t.TrimStart('>', '<', '=')).Distinct().ToList();
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
