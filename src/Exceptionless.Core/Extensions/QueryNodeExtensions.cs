using Foundatio.Parsers.LuceneQueries.Nodes;

namespace Exceptionless.Core.Extensions;

public static class QueryNodeExtensions
{
    public static GroupNode? GetParent(this IQueryNode? node, Func<GroupNode, bool> condition)
    {
        if (node is null)
            return null;

        IQueryNode queryNode = node;
        do
        {
            if (queryNode is GroupNode groupNode && condition(groupNode))
                return groupNode;

            queryNode = queryNode.Parent;
        }
        while (queryNode is not null);

        return null;
    }
}
