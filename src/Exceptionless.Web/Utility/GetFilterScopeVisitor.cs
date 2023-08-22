﻿using Foundatio.Parsers.LuceneQueries;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;

namespace Exceptionless.Web.Utility;

public class GetFilterScopeVisitor : QueryNodeVisitorWithResultBase<FilterScope>
{
    private readonly FilterScope _scope = new();
    private static readonly LuceneQueryParser _parser = new();

    public override void Visit(TermNode node, IQueryVisitorContext context)
    {
        if (String.IsNullOrEmpty(node.Field) || !_scope.IsScopable)
            return;

        if (node.Field.Equals("organization"))
        {
            if (!_scope.HasScope)
                _scope.OrganizationId = node.UnescapedTerm;
            else // found dupe, mark filter as not scopable
                _scope.IsScopable = false;
        }
        else if (node.Field.Equals("project"))
        {
            if (!_scope.HasScope)
                _scope.ProjectId = node.UnescapedTerm;
            else // found dupe, mark filter as not scopable
                _scope.IsScopable = false;
        }
        else if (node.Field.Equals("stack"))
        {
            if (!_scope.HasScope)
                _scope.StackId = node.UnescapedTerm;
            else // found dupe, mark filter as not scopable
                _scope.IsScopable = false;
        }
    }

    public override Task<FilterScope> AcceptAsync(IQueryNode node, IQueryVisitorContext? context)
    {
        node.AcceptAsync(this, context);
        return Task.FromResult(_scope);
    }

    public static FilterScope Run(string filter)
    {
        var node = _parser.Parse(filter);
        return new GetFilterScopeVisitor().AcceptAsync(node, null).GetAwaiter().GetResult();
    }
}

public record FilterScope
{
    public string? OrganizationId { get; set; }
    public string? ProjectId { get; set; }
    public string? StackId { get; set; }
    public bool IsScopable { get; set; } = true;
    public bool HasScope => OrganizationId is not null || ProjectId is not null || StackId is not null;
}
