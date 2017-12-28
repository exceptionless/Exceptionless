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
            if (node.Left is TermNode leftTermNode && leftTermNode.Field == null)
                childTerms.Add(leftTermNode.Term);

            if (node.Left is TermRangeNode leftTermRangeNode && leftTermRangeNode.Field == null) {
                childTerms.Add(leftTermRangeNode.Min);
                childTerms.Add(leftTermRangeNode.Max);
            }

            if (node.Right is TermNode rightTermNode && rightTermNode.Field == null)
                childTerms.Add(rightTermNode.Term);

            if (node.Right is TermRangeNode rightTermRangeNode && rightTermRangeNode.Field == null) {
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

            node.Field = GetCustomFieldName(node.Field, new [] { node.Term });
            return Task.CompletedTask;
        }

        public override Task VisitAsync(TermRangeNode node, IQueryVisitorContext context) {
            node.Field = GetCustomFieldName(node.Field, new [] { node.Min, node.Max });
            return Task.CompletedTask;
        }

        public override Task VisitAsync(ExistsNode node, IQueryVisitorContext context) {
            node.Field = GetCustomFieldName(node.Field, Array.Empty<string>());
            return Task.CompletedTask;
        }

        public override Task VisitAsync(MissingNode node, IQueryVisitorContext context) {
            node.Field = GetCustomFieldName(node.Field, Array.Empty<string>());
            return Task.CompletedTask;
        }

        private string GetCustomFieldName(string field, string[] terms) {
            if (String.IsNullOrEmpty(field))
                return null;

            var parts = field.Split('.');
            if (parts.Length != 2 || (parts.Length == 2 && parts[1].StartsWith("@")))
                return field;

            if (String.Equals(parts[0], "data", StringComparison.OrdinalIgnoreCase)) {
                string termType;
                if (String.Equals(parts[1], Event.KnownDataKeys.SessionEnd, StringComparison.OrdinalIgnoreCase))
                    termType = "d";
                else if (String.Equals(parts[1], Event.KnownDataKeys.SessionHasError, StringComparison.OrdinalIgnoreCase))
                    termType = "b";
                else
                    termType = GetTermType(terms);

                field = $"idx.{parts[1].ToLowerInvariant()}-{termType}";
            } else if (String.Equals(parts[0], "ref", StringComparison.OrdinalIgnoreCase)) {
                field = $"idx.{parts[1].ToLowerInvariant()}-r";
            }

            return field;
        }

        private static string GetTermType(string[] terms) {
            string termType = "s";

            var trimmedTerms = terms.Where(t => t != null).Distinct().ToList();
            foreach (string term in trimmedTerms) {
                if (term.StartsWith("*"))
                    continue;

                if (Boolean.TryParse(term, out var boolResult))
                    termType = "b";
                else if (term.IsNumeric())
                    termType = "n";
                else if (DateTime.TryParse(term, out var dateResult))
                    termType = "d";

                break;
            }

            // Some terms can be a string date range: [now TO now/d+1d}
            if (String.Equals(termType, "s") && trimmedTerms.Count > 0 && trimmedTerms.All(t => String.Equals(t, "now", StringComparison.OrdinalIgnoreCase) || t.StartsWith("now/", StringComparison.OrdinalIgnoreCase)))
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