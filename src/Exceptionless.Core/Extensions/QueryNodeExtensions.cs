using System;
using Foundatio.Parsers.LuceneQueries.Nodes;

namespace Exceptionless.Core.Extensions {
    public static class QueryNodeExtensions {
        public static GroupNode GetParent(this IQueryNode node, Func<GroupNode, bool> condition) {
            if (node == null)
                return null;

            IQueryNode queryNode = node;
            do {
                GroupNode groupNode = queryNode as GroupNode;
                if (groupNode != null && condition(groupNode))
                    return groupNode;

                queryNode = queryNode.Parent;
            }
            while (queryNode != null);

            return null;
        }
    }
}
