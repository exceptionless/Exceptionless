using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;
using Elastic.Clients.Elasticsearch.QueryDsl;

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

        Query query;
        if (isFixed)
        {
            query = new ExistsQuery { Field = _dateFixedFieldName };
        }
        else
        {
            query = new BoolQuery
            {
                MustNot = new Query[] { new ExistsQuery { Field = _dateFixedFieldName } }
            };
        }
        
        node.SetQuery(query);

        return Task.FromResult<IQueryNode>(node);
    }
}
