using Exceptionless.Core.Services;
using Foundatio.Parsers.LuceneQueries.Extensions;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;

namespace Exceptionless.Core.Repositories.Queries.Visitors;

/// <summary>
/// Expands session system-field filters across the legacy named index field and the current pooled slot.
/// </summary>
public sealed class EventSystemFieldCompatibilityQueryVisitor : ChainableMutatingQueryVisitor
{
    public override Task<IQueryNode?> VisitAsync(GroupNode node, IQueryVisitorContext context)
    {
        if (TryGetLegacyField(node.Field, out _))
            return Task.FromResult<IQueryNode?>(Expand(node, GroupOperator.Or));

        return base.VisitAsync(node, context);
    }

    public override IQueryNode Visit(TermNode node, IQueryVisitorContext context)
        => Expand(node, GroupOperator.Or);

    public override IQueryNode Visit(TermRangeNode node, IQueryVisitorContext context)
        => Expand(node, GroupOperator.Or);

    public override IQueryNode Visit(ExistsNode node, IQueryVisitorContext context)
        => Expand(node, GroupOperator.Or);

    public override IQueryNode Visit(MissingNode node, IQueryVisitorContext context)
        => Expand(node, GroupOperator.And);

    private static IQueryNode Expand(IQueryNode node, GroupOperator compatibilityOperator)
    {
        if (node is not IFieldQueryNode fieldNode || !TryGetLegacyField(fieldNode.Field, out string legacyField))
            return node;

        var currentNode = node.Clone();
        var currentFieldNode = (IFieldQueryNode)currentNode;
        currentFieldNode.IsNegated = null;
        currentFieldNode.Prefix = null;

        var legacyNode = node.Clone();
        var legacyFieldNode = (IFieldQueryNode)legacyNode;
        legacyFieldNode.Field = legacyField;
        legacyFieldNode.IsNegated = null;
        legacyFieldNode.Prefix = null;

        return node.ReplaceSelf(new GroupNode
        {
            HasParens = true,
            IsNegated = fieldNode.IsNegated,
            Prefix = fieldNode.Prefix,
            Operator = compatibilityOperator,
            Left = currentNode,
            Right = legacyNode
        });
    }

    private static bool TryGetLegacyField(string? field, out string legacyField)
    {
        legacyField = String.Empty;
        if (String.IsNullOrWhiteSpace(field))
            return false;

        string? systemFieldName = null;
        if (field.StartsWith("data.", StringComparison.OrdinalIgnoreCase))
            systemFieldName = field[5..];
        else if (field.StartsWith("idx.", StringComparison.OrdinalIgnoreCase))
            systemFieldName = field[4..];
        else if (field.StartsWith("ref.", StringComparison.OrdinalIgnoreCase))
            systemFieldName = $"@ref:{field[4..]}";

        if (systemFieldName is null || !EventCustomFieldService.TryGetSystemField(systemFieldName, out var descriptor))
            return false;

        legacyField = $"idx.{descriptor.LegacyIdxField}";
        return true;
    }
}
