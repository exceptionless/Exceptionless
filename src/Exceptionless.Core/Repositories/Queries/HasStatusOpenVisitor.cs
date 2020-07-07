using System;
using System.Threading.Tasks;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;

namespace Exceptionless.Core.Repositories.Queries {
    public class HasStatusOpenVisitor : QueryNodeVisitorWithResultBase<bool> {
        private bool _hasStatusOpen;

        public override void Visit(TermNode node, IQueryVisitorContext context) {
            if (node.IsNegated.GetValueOrDefault() || !String.Equals(node.Field, "status", StringComparison.OrdinalIgnoreCase)) 
                return;
            
            if (String.Equals(node.Term, "open", StringComparison.OrdinalIgnoreCase))
                _hasStatusOpen = true;
        }

        public override Task<bool> AcceptAsync(IQueryNode node, IQueryVisitorContext context) {
            node.AcceptAsync(this, context);
            return Task.FromResult(_hasStatusOpen);
        }

        public static Task<bool> RunAsync(IQueryNode node, IQueryVisitorContext context = null) {
            return new HasStatusOpenVisitor().AcceptAsync(node, context);
        }

        public static bool Run(IQueryNode node, IQueryVisitorContext context = null) {
            return RunAsync(node, context).GetAwaiter().GetResult();
        }
    }
}