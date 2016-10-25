using System;
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

        public override void Visit(TermNode node, IQueryVisitorContext context) {
            if (!String.Equals(node.Field, "fixed", StringComparison.OrdinalIgnoreCase))
                return;

            bool isFixed;
            if (!Boolean.TryParse(node.Term, out isFixed))
                return;

            var query = new ExistsQuery { Field = _dateFixedFieldName };
            node.SetQuery(isFixed ? query : !query);
        }
    }
}