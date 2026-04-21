using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;

namespace Exceptionless.Core.Repositories.Queries;

public class EventFieldsQueryVisitor : ChainableQueryVisitor
{
    public override async Task VisitAsync(GroupNode node, IQueryVisitorContext context)
    {
        var childTerms = new List<string>();
        if (node.Left is TermNode { Field: null, Term: not null } leftTermNode)
            childTerms.Add(leftTermNode.Term);

        if (node.Left is TermRangeNode { Field: null } leftTermRangeNode)
        {
            if (leftTermRangeNode.Min is not null)
                childTerms.Add(leftTermRangeNode.Min);
            if (leftTermRangeNode.Max is not null)
                childTerms.Add(leftTermRangeNode.Max);
        }

        if (node.Right is TermNode { Field: null, Term: not null } rightTermNode)
            childTerms.Add(rightTermNode.Term);

        if (node.Right is TermRangeNode { Field: null } rightTermRangeNode)
        {
            if (rightTermRangeNode.Min is not null)
                childTerms.Add(rightTermRangeNode.Min);
            if (rightTermRangeNode.Max is not null)
                childTerms.Add(rightTermRangeNode.Max);
        }

        node.Field = GetCustomFieldName(node.Field, childTerms.ToArray()) ?? node.Field;

        // Propagate resolved field to child TermRangeNodes that lack a field name.
        // Without this, Foundatio.Parsers' DefaultQueryNodeExtensions.GetDefaultQueryAsync
        // throws when creating Field objects for grouped range queries like data.age:(>30 AND <=40).
        if (!String.IsNullOrEmpty(node.Field))
        {
            if (node.Left is TermRangeNode { Field: null or "" } leftRange)
                leftRange.Field = node.Field;
            if (node.Right is TermRangeNode { Field: null or "" } rightRange)
                rightRange.Field = node.Field;
        }

        foreach (var child in node.Children)
            await child.AcceptAsync(this, context);
    }

    public override Task VisitAsync(TermNode node, IQueryVisitorContext context)
    {
        // using all fields search
        if (String.IsNullOrEmpty(node.Field))
        {
            return Task.CompletedTask;
        }

        node.Field = GetCustomFieldName(node.Field, [node.Term]);
        return Task.CompletedTask;
    }

    public override Task VisitAsync(TermRangeNode node, IQueryVisitorContext context)
    {
        node.Field = GetCustomFieldName(node.Field, [node.Min, node.Max]);
        return Task.CompletedTask;
    }

    public override Task VisitAsync(ExistsNode node, IQueryVisitorContext context)
    {
        node.Field = GetCustomFieldName(node.Field, []);
        return Task.CompletedTask;
    }

    public override Task VisitAsync(MissingNode node, IQueryVisitorContext context)
    {
        node.Field = GetCustomFieldName(node.Field, []);
        return Task.CompletedTask;
    }

    private string? GetCustomFieldName(string? field, string?[] terms)
    {
        if (String.IsNullOrEmpty(field))
            return null;

        string[] parts = field.Split('.');
        if (parts.Length != 2 || (parts.Length == 2 && parts[1].StartsWith("@")))
            return field;

        if (String.Equals(parts[0], "data", StringComparison.OrdinalIgnoreCase))
        {
            string termType;
            if (String.Equals(parts[1], Event.KnownDataKeys.SessionEnd, StringComparison.OrdinalIgnoreCase))
                termType = "d";
            else if (String.Equals(parts[1], Event.KnownDataKeys.SessionHasError, StringComparison.OrdinalIgnoreCase))
                termType = "b";
            else
                termType = GetTermType(terms);

            field = $"idx.{parts[1].ToLowerInvariant()}-{termType}";
        }
        else if (String.Equals(parts[0], "ref", StringComparison.OrdinalIgnoreCase))
        {
            field = $"idx.{parts[1].ToLowerInvariant()}-r";
        }

        return field;
    }

    private static string GetTermType(string?[] terms)
    {
        string termType = "s";

        var trimmedTerms = terms.OfType<string>().Distinct().ToList();
        foreach (string term in trimmedTerms)
        {
            if (term.StartsWith('*'))
                continue;

            if (Boolean.TryParse(term, out bool _))
                termType = "b";
            else if (term.IsNumeric())
                termType = "n";
            else if (DateTime.TryParse(term, out DateTime _))
                termType = "d";

            break;
        }

        // Some terms can be a string date range: [now TO now/d+1d}
        if (String.Equals(termType, "s") && trimmedTerms.Count > 0 && trimmedTerms.All(t => String.Equals(t, "now", StringComparison.OrdinalIgnoreCase) || t.StartsWith("now/", StringComparison.OrdinalIgnoreCase)))
            termType = "d";

        return termType;
    }

    public static Task<IQueryNode?> RunAsync(IQueryNode node, IQueryVisitorContext? context = null)
    {
        return new EventFieldsQueryVisitor().AcceptAsync(node, context ?? new QueryVisitorContext());
    }

    public static IQueryNode? Run(IQueryNode node, IQueryVisitorContext? context = null)
    {
        return RunAsync(node, context).GetAwaiter().GetResult();
    }
}
