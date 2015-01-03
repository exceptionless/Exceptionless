using System;
using System.Collections.Generic;
using Exceptionless.LuceneQueryParser;
using Exceptionless.LuceneQueryParser.Nodes;
using Exceptionless.LuceneQueryParser.Visitor;

namespace Exceptionless.Core.Filter {
    public class QueryValidationVisitor : QueryNodeVisitorBase {
        private readonly HashSet<string> _freeFields;

        public QueryValidationVisitor(HashSet<string> freeFields) {
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

            base.Visit(node);
        }

        public bool UsesPremiumFeatures { get; set; }

        public static QueryValidationResult Validate(string query) {
            if (String.IsNullOrEmpty(query))
                return new QueryValidationResult { IsValid = true };

            GroupNode result;
            try {
                var parser = new QueryParser();
                result = parser.Parse(query);
            } catch (Exception ex) {
                return new QueryValidationResult { Message = ex.Message };
            }

            var validator = new QueryValidationVisitor(new HashSet<string> { "hidden", "fixed", "type" });
            result.Accept(validator);

            return new QueryValidationResult { IsValid = true, UsesPremiumFeatures = validator.UsesPremiumFeatures };
        }
    }

    public class QueryValidationResult {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public bool UsesPremiumFeatures { get; set; }
    }
}
