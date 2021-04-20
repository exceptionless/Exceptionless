using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Parsers.ElasticQueries.Visitors;
using Foundatio.Parsers.LuceneQueries;
using Foundatio.Parsers.LuceneQueries.Extensions;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;

namespace Exceptionless.Core.Repositories.Queries {
    public class EventStackFilter {
        private readonly ISet<string> _stackNonInvertedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "organization_id", StackIndex.Alias.OrganizationId,
            "project_id", StackIndex.Alias.ProjectId,
            EventIndex.Alias.StackId, "stack_id",
            StackIndex.Alias.Type,
        };

        private readonly ISet<string> _stackAndEventFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "organization_id", StackIndex.Alias.OrganizationId,
            "project_id", StackIndex.Alias.ProjectId,
            EventIndex.Alias.StackId, "stack_id",
            StackIndex.Alias.Type,
            StackIndex.Alias.Tags, "tags"
        };

        private readonly ISet<string> _stackOnlyFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            StackIndex.Alias.LastOccurrence, "last_occurrence",
            StackIndex.Alias.References, "references",
            "status",
            "snooze_until_utc",
            StackIndex.Alias.SignatureHash, "signature_hash",
            "title",
            "description",
            "first_occurrence",
            StackIndex.Alias.DateFixed, "date_fixed",
            StackIndex.Alias.FixedInVersion, "fixed_in_version",
            StackIndex.Alias.OccurrencesAreCritical, "occurrences_are_critical",
            StackIndex.Alias.TotalOccurrences, "total_occurrences"
        };

        private readonly ISet<string> _stackOnlySpecialFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            StackIndex.Alias.IsFixed, "is_fixed",
            StackIndex.Alias.IsRegressed, "is_regressed",
            StackIndex.Alias.IsHidden, "is_hidden"
        };

        private readonly LuceneQueryParser _parser;
        private readonly ChainedQueryVisitor _eventQueryVisitor;
        private readonly ChainedQueryVisitor _stackQueryVisitor;
        private readonly ChainedQueryVisitor _invertedStackQueryVisitor;

        public EventStackFilter() {
            var stackOnlyFields = _stackOnlyFields.Union(_stackOnlySpecialFields);
            var stackFields = stackOnlyFields.Union(_stackAndEventFields);

            _parser = new LuceneQueryParser();
            _eventQueryVisitor = new ChainedQueryVisitor();
            _eventQueryVisitor.AddVisitor(new RemoveFieldsQueryVisitor(stackOnlyFields));
            _eventQueryVisitor.AddVisitor(new CleanupQueryVisitor());

            _stackQueryVisitor = new ChainedQueryVisitor();
            // remove everything not in the stack fields list
            _stackQueryVisitor.AddVisitor(new RemoveFieldsQueryVisitor(f => !stackFields.Contains(f)));
            _stackQueryVisitor.AddVisitor(new CleanupQueryVisitor());
            // handles stack special fields and changing event field names to their stack equivalent
            _stackQueryVisitor.AddVisitor(new StackFilterQueryVisitor());
            _stackQueryVisitor.AddVisitor(new CleanupQueryVisitor());

            _invertedStackQueryVisitor = new ChainedQueryVisitor();
            // remove everything not in the stack fields list
            _invertedStackQueryVisitor.AddVisitor(new RemoveFieldsQueryVisitor(f => !stackFields.Contains(f)));
            _invertedStackQueryVisitor.AddVisitor(new CleanupQueryVisitor());
            // handles stack special fields and changing event field names to their stack equivalent
            _invertedStackQueryVisitor.AddVisitor(new StackFilterQueryVisitor());
            _invertedStackQueryVisitor.AddVisitor(new CleanupQueryVisitor());
            // inverts the filter
            _invertedStackQueryVisitor.AddVisitor(new InvertQueryVisitor(_stackNonInvertedFields));
            _invertedStackQueryVisitor.AddVisitor(new CleanupQueryVisitor());
        }

        public async Task<string> GetEventFilterAsync(string query, IQueryVisitorContext context = null) {
            context ??= new ElasticQueryVisitorContext();
            var result = await _parser.ParseAsync(query, context);
            await _eventQueryVisitor.AcceptAsync(result, context);
            return result.ToString();
        }

        public async Task<StackFilter> GetStackFilterAsync(string query, IQueryVisitorContext context = null) {
            context ??= new ElasticQueryVisitorContext();
            var result = await _parser.ParseAsync(query, context);
            var invertedResult = result.Clone();

            result = await _stackQueryVisitor.AcceptAsync(result, context);
            invertedResult = await _invertedStackQueryVisitor.AcceptAsync(invertedResult, context);

            return new StackFilter {
                Filter = result.ToString(),
                InvertedFilter = invertedResult.ToString(),
                HasStatus = context.GetBoolean(nameof(StackFilter.HasStatus)),
                HasStackIds = context.GetBoolean(nameof(StackFilter.HasStackIds)),
                HasStatusOpen = context.GetBoolean(nameof(StackFilter.HasStatusOpen))
            };
        }
    }

    public class StackFilterQueryVisitor : ChainableQueryVisitor {
        public override Task<IQueryNode> VisitAsync(TermNode node, IQueryVisitorContext context) {
            IQueryNode result = node;

            // don't include terms without fields
            if (node.Field == null) {
                node.RemoveSelf();
                return Task.FromResult<IQueryNode>(null);
            }

            // process special stack fields
            switch (node.Field?.ToLowerInvariant()) {
                case EventIndex.Alias.StackId:
                case "stack_id":
                    node.Field = "id";
                    break;
                case "is_fixed":
                case StackIndex.Alias.IsFixed:
                    bool isFixed = Boolean.TryParse(node.Term, out bool temp) && temp;
                    node.Field = "status";
                    node.Term = "fixed";
                    node.IsNegated = !isFixed;
                    break;
                case "is_regressed":
                case StackIndex.Alias.IsRegressed:
                    bool isRegressed = Boolean.TryParse(node.Term, out bool regressed) && regressed;
                    node.Field = "status";
                    node.Term = "regressed";
                    node.IsNegated = !isRegressed;
                    break;
                case "is_hidden":
                case StackIndex.Alias.IsHidden:
                    bool isHidden = Boolean.TryParse(node.Term, out bool hidden) && hidden;
                    if (isHidden) {
                        var isHiddenNode = new GroupNode {
                            HasParens = true,
                            IsNegated = true,
                            Operator = GroupOperator.Or,
                            Left = new TermNode { Field = "status", Term = "open" },
                            Right = new TermNode { Field = "status", Term = "regressed" }
                        };

                        result = node.ReplaceSelf(isHiddenNode);

                        break;
                    } else {
                        var notHiddenNode = new GroupNode {
                            HasParens = true,
                            Operator = GroupOperator.Or,
                            Left = new TermNode { Field = "status", Term = "open" },
                            Right = new TermNode { Field = "status", Term = "regressed" }
                        };

                        result = node.ReplaceSelf(notHiddenNode);

                        break;
                    }
            }

            if (result is TermNode termNode) {
                if (String.Equals(termNode.Field, "status", StringComparison.OrdinalIgnoreCase)) {
                    context.SetValue(nameof(StackFilter.HasStatus), true);

                    if (!termNode.IsNegated.GetValueOrDefault() && String.Equals(termNode.Term, "open", StringComparison.OrdinalIgnoreCase))
                        context.SetValue(nameof(StackFilter.HasStatusOpen), true);
                }

                if ((String.Equals(termNode.Field, EventIndex.Alias.StackId, StringComparison.OrdinalIgnoreCase)
                    || String.Equals(termNode.Field, "stack_id", StringComparison.OrdinalIgnoreCase))
                    && !String.IsNullOrEmpty(termNode.Term)) {
                    context.SetValue(nameof(StackFilter.HasStackIds), true);
                }
            }

            return Task.FromResult<IQueryNode>(result);
        }

        public override Task<IQueryNode> AcceptAsync(IQueryNode node, IQueryVisitorContext context) {
            return node.AcceptAsync(this, context);
        }
    }

    public class StackFilter {
        public string Filter { get; set; }
        public string InvertedFilter { get; set; }
        public bool HasStatus { get; set; }
        public bool HasStatusOpen { get; set; }
        public bool HasStackIds { get; set; }
    }
}