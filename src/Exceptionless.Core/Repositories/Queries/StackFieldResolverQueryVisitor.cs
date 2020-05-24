using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;

namespace Exceptionless.Core.Repositories.Queries
{
    public class StackFieldResolverQueryVisitor : ChainableQueryVisitor {
        private readonly IDictionary<string, string> _fieldMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { StackIndex.Alias.FirstOccurrence, "first_occurrence" },
            { "first_occurrence", "first_occurrence"},
            { StackIndex.Alias.LastOccurrence, "last_occurrence" },
            { "last_occurrence", "last_occurrence" },
            { "references", "references" },
            { StackIndex.Alias.References, "references" },
            { "status", "status" },
            { "snooze_until_utc", "snooze_until_utc" },
            { "signature_hash", "signature_hash" },
            { StackIndex.Alias.SignatureHash, "signature_hash" },
            { "title", "title" },
            { "description", "description" },
            { StackIndex.Alias.DateFixed, "date_fixed" },
            { "date_fixed", "date_fixed" },
            { StackIndex.Alias.FixedInVersion, "fixed_in_version" },
            { "fixed_in_version", "fixed_in_version" },
            { StackIndex.Alias.OccurrencesAreCritical, "occurrences_are_critical" },
            { "occurrences_are_critical", "occurrences_are_critical" },
            { StackIndex.Alias.TotalOccurrences, "total_occurrences" },
            { "total_occurrences", "total_occurrences" }
        };

        public StackFieldResolverQueryVisitor() {
        }
        
        public override Task VisitAsync(GroupNode node, IQueryVisitorContext context) {
            ResolveField(node, context);

            return base.VisitAsync(node, context);
        }

        public override void Visit(TermNode node, IQueryVisitorContext context) {
            ResolveField(node, context);
        }

        public override void Visit(TermRangeNode node, IQueryVisitorContext context) {
            ResolveField(node, context);
        }

        public override void Visit(ExistsNode node, IQueryVisitorContext context) {
            ResolveField(node, context);
        }

        public override void Visit(MissingNode node, IQueryVisitorContext context) {
            ResolveField(node, context);
        }

        private void ResolveField(IFieldQueryNode node, IQueryVisitorContext context) {
            if (node.Parent == null || node.Field == null)
                return;

            if (!(node.Parent is GroupNode groupNode) || String.Equals(groupNode.Field, EventJoinFilterVisitor.StackFieldName))
                return;

            GroupNode stackNode = null;
            if (_fieldMapping.TryGetValue(node.Field, out string resolvedField)) {
                stackNode = new GroupNode { Field = EventJoinFilterVisitor.StackFieldName, HasParens = true, Left = node };
                node.Field = resolvedField;

            } else if (node is TermNode termNode) {
                switch (node.Field?.ToLowerInvariant()) {
                    case "is_fixed":
                    case StackIndex.Alias.IsFixed:
                        bool isFixed = Boolean.TryParse(termNode.Term, out bool temp) && temp;
                        stackNode = new GroupNode {
                            Field = EventJoinFilterVisitor.StackFieldName,
                            HasParens = true,
                            Left = new TermNode {
                                Field = "status",
                                Term = "fixed",
                                IsNegated = !isFixed
                            }
                        };
                        break;
                    
                    case "is_regressed":
                    case StackIndex.Alias.IsRegressed:
                        bool isRegressed = Boolean.TryParse(termNode.Term, out bool regressed) && regressed;
                        stackNode = new GroupNode {
                            Field = EventJoinFilterVisitor.StackFieldName,
                            HasParens = true,
                            Left = new TermNode {
                                Field = "status",
                                Term = "regressed",
                                IsNegated = !isRegressed
                            }
                        };
                        
                        break;
                    
                    case "is_hidden":
                    case StackIndex.Alias.IsHidden:
                        bool isHidden = Boolean.TryParse(termNode.Term, out bool hidden) && hidden;
                        stackNode = new GroupNode {
                            Field = EventJoinFilterVisitor.StackFieldName,
                            HasParens = true,
                            Operator = isHidden ? GroupOperator.And : GroupOperator.Or,
                            Left = new TermNode {Field = "status", Term = "open", IsNegated = !isHidden},
                            Right = new TermNode {Field = "status", Term = "regressed", IsNegated = !isHidden}
                        };
                        break;
                }
            }

            if (stackNode == null)
                return;

            if (groupNode.Left == node)
                groupNode.Left = stackNode;
            else
                groupNode.Right = stackNode;
        }

        public override async Task<IQueryNode> AcceptAsync(IQueryNode node, IQueryVisitorContext context) {
            await node.AcceptAsync(this, context).ConfigureAwait(false);
            return node;
        }

        public static Task<IQueryNode> RunAsync(IQueryNode node, IQueryVisitorContext context = null) {
            return new StackFieldResolverQueryVisitor().AcceptAsync(node, context);
        }

        public static IQueryNode Run(IQueryNode node, IQueryVisitorContext context = null) {
            return RunAsync(node, context).GetAwaiter().GetResult();
        }
    }
}