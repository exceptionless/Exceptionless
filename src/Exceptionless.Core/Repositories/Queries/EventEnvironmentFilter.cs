using Foundatio.Parsers.LuceneQueries;
using Foundatio.Parsers.LuceneQueries.Nodes;

namespace Exceptionless.Core.Repositories.Queries;

/// <summary>
/// Preserves deployment scope when counting all project users, including users without matching errors.
/// </summary>
public static class EventEnvironmentFilter
{
    public static async Task<string?> GetAsync(string? filter)
    {
        if (String.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        return GetConstraint(await new LuceneQueryParser().ParseAsync(filter));
    }

    private static string? GetConstraint(IQueryNode? node, bool negate = false)
    {
        if (node is not IFieldQueryNode field)
        {
            return null;
        }

        if (String.Equals(field.UnescapedField, "environment", StringComparison.OrdinalIgnoreCase))
        {
            return negate ? $"NOT ({node})" : node.ToString();
        }

        if (field.Field is not null || node is not GroupNode group)
        {
            return null;
        }

        negate ^= group.IsNegated == true || group.Prefix == "-";
        if (group.Left is null)
        {
            return GetConstraint(group.Right, negate);
        }
        if (group.Right is null)
        {
            return GetConstraint(group.Left, negate);
        }

        string? left = GetConstraint(group.Left, negate);
        string? right = GetConstraint(group.Right, negate);
        bool isOr = (group.Operator == GroupOperator.Or) ^ negate;
        // An unrelated OR branch permits every environment. Unrelated AND clauses do not restrict it.
        if (isOr && (left is null || right is null))
        {
            return null;
        }
        if (left is null)
        {
            return right;
        }
        if (right is null)
        {
            return left;
        }

        return $"({left} {(isOr ? "OR" : "AND")} {right})";
    }
}
