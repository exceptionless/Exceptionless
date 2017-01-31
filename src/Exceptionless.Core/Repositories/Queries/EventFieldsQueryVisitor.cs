using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;

namespace Exceptionless.Core.Repositories.Queries {
    public class EventFieldsQueryVisitor : ChainableQueryVisitor {
        public override async Task VisitAsync(GroupNode node, IQueryVisitorContext context) {
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

        public override Task VisitAsync(TermNode node, IQueryVisitorContext context) {
            // using all fields search
            if (String.IsNullOrEmpty(node.Field)) {
                return Task.CompletedTask;
            }

            node.Field = GetCustomFieldName(node.Field, node.Term);
            return Task.CompletedTask;
        }

        public override Task VisitAsync(TermRangeNode node, IQueryVisitorContext context) {
            node.Field = GetCustomFieldName(node.Field, node.Min, node.Max);
            return Task.CompletedTask;
        }

        public override Task VisitAsync(ExistsNode node, IQueryVisitorContext context) {
            node.Field = GetCustomFieldName(node.Field);
            return Task.CompletedTask;
        }

        public override Task VisitAsync(MissingNode node, IQueryVisitorContext context) {
            node.Field = GetCustomFieldName(node.Field);
            return Task.CompletedTask;
        }

        private string GetCustomFieldName(string field, params string[] terms) {
            if (String.IsNullOrEmpty(field))
                return null;

            string[] parts = field.Split('.');
            if (parts.Length != 2 || (parts.Length == 2 && IsKnownDataKey(parts[1])))
                return field;

            if (String.Equals(parts[0], "data")) {
                string termType = GetTermType(terms);
                field = $"idx.{parts[1].ToLowerInvariant()}-{termType}";
            } else if (String.Equals(parts[0], "ref")) {
                field = $"idx.{parts[1].ToLowerInvariant()}-r";
            }

            return field;
        }

        private bool IsKnownDataKey(string field) {
            return field.StartsWith("@") || String.Equals(field, Event.KnownDataKeys.SessionEnd, StringComparison.OrdinalIgnoreCase) || String.Equals(field, Event.KnownDataKeys.SessionHasError, StringComparison.OrdinalIgnoreCase);
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

        public static Task<IQueryNode> RunAsync(IQueryNode node, IQueryVisitorContext context = null) {
            return new EventFieldsQueryVisitor().AcceptAsync(node, context);
        }

        public static IQueryNode Run(IQueryNode node, IQueryVisitorContext context = null) {
            return RunAsync(node, context).GetAwaiter().GetResult();
        }
    }
}