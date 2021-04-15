using System;
using System.Threading.Tasks;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Nest;

namespace Exceptionless.Core.Repositories.Queries {
    public class StackDateFixedQueryVisitor : ChainableQueryVisitor {
        private readonly string _dateFixedFieldName;
        public StackDateFixedQueryVisitor(string dateFixedFieldName) {
            if (String.IsNullOrEmpty(dateFixedFieldName))
                throw new ArgumentNullException(nameof(dateFixedFieldName));

            _dateFixedFieldName = dateFixedFieldName;
        }

        public override Task<IQueryNode> VisitAsync(TermNode node, IQueryVisitorContext context) {
            if (!String.Equals(node.Field, "fixed", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<IQueryNode>(node);

            if (!Boolean.TryParse(node.Term, out bool isFixed))
                return Task.FromResult<IQueryNode>(node);

            var query = new ExistsQuery { Field = _dateFixedFieldName };
            node.SetQuery(isFixed ? query : !query);

            return Task.FromResult<IQueryNode>(node);
        }
    }
}