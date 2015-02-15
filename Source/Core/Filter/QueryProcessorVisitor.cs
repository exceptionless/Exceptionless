using System;
using System.Collections.Generic;
using Exceptionless.Core.Extensions;
using Exceptionless.LuceneQueryParser;
using Exceptionless.LuceneQueryParser.Nodes;
using Exceptionless.LuceneQueryParser.Visitor;

namespace Exceptionless.Core.Filter {
    public class QueryProcessor {
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

            var validator = new QueryProcessorVisitor(new HashSet<string> { "hidden", "fixed", "type" });
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

            var validator = new QueryProcessorVisitor(new HashSet<string> { "hidden", "fixed", "type" });
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
            if (node.Field != null && !_freeFields.Contains(node.Field.Field))
                UsesPremiumFeatures = true;

            base.Visit(node);
        }

        public override void Visit(TermNode node) {
            // using a field not in the free list
            if (node.Field != null && !_freeFields.Contains(node.Field.Field))
                UsesPremiumFeatures = true;

            // using all fields search
            if (node.Field == null || String.IsNullOrEmpty(node.Field.Field))
                UsesPremiumFeatures = true;

            if (node.Field != null && node.Field.Field.StartsWith("data.")) {
                string term = node.Term ?? node.TermMin ?? node.TermMax;
                string termType = "s";
                bool boolResult;
                DateTime dateResult;
                if (Boolean.TryParse(term, out boolResult))
                    termType = "b";
                else if (term.IsNumeric())
                    termType = "n";
                else if (DateTime.TryParse(term, out dateResult))
                    termType = "d";

                UsesDataFields = true;
                node.Field.Field = "idx." + node.Field.Field.ToLower().Substring(5) + "-" + termType;
            }

            base.Visit(node);
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
