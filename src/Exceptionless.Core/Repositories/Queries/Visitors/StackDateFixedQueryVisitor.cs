using Elastic.Clients.Elasticsearch.QueryDsl;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;

namespace Exceptionless.Core.Repositories.Queries;

public class StackDateFixedQueryVisitor : ChainableQueryVisitor
{
    private readonly string _dateFixedFieldName;
    public StackDateFixedQueryVisitor(string dateFixedFieldName)
    {
        ArgumentException.ThrowIfNullOrEmpty(dateFixedFieldName);

        _dateFixedFieldName = dateFixedFieldName;
    }

    public override Task<IQueryNode> VisitAsync(TermNode node, IQueryVisitorContext context)
    {
        if (!String.Equals(node.Field, "fixed", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<IQueryNode>(node);

        if (!Boolean.TryParse(node.Term, out bool isFixed))
            return Task.FromResult<IQueryNode>(node);

        var existsQuery = new ExistsQuery { Field = _dateFixedFieldName };
        Query query = isFixed
            ? existsQuery
            : new BoolQuery { MustNot = new Query[] { existsQuery } };

        node.SetQuery(query);

        return Task.FromResult<IQueryNode>(node);
    }
}
