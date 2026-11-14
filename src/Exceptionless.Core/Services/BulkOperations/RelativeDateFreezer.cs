using System.Globalization;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Parsers.LuceneQueries;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;

namespace Exceptionless.Core.Services.BulkOperations;

/// <summary>
/// Pins relative dates in a filter (for example <c>data.date:[now/d-4d TO now/d+1d}</c>) to a fixed time, so a bulk
/// operation keeps selecting the same events no matter when each batch runs. Only date fields are changed: a string range
/// such as <c>environment:[now TO z]</c> is left alone.
/// </summary>
public static class RelativeDateFreezer
{
    /// <param name="filter">The filter to pin.</param>
    /// <param name="utcNow">The time that <c>now</c> stands for.</param>
    /// <param name="isDateFieldAsync">Whether a built-in (non custom data) field is mapped as a date.</param>
    public static async Task<string?> FreezeAsync(string? filter, DateTime utcNow, Func<string, ValueTask<bool>> isDateFieldAsync)
    {
        if (String.IsNullOrWhiteSpace(filter) || filter.IndexOf("now", StringComparison.OrdinalIgnoreCase) < 0)
            return filter;

        var result = await new LuceneQueryParser().ParseAsync(filter);
        if (result is null)
            return filter;

        await new FreezeRelativeDatesVisitor(utcNow, isDateFieldAsync).AcceptAsync(result, new QueryVisitorContext());
        return result.ToString();
    }

    private sealed class FreezeRelativeDatesVisitor(DateTime utcNow, Func<string, ValueTask<bool>> isDateFieldAsync) : ChainableQueryVisitor
    {
        // Elasticsearch date math anchors an expression on a date with ||, for example 2026-11-03T04:00:00.000Z||/d-4d.
        private readonly string _anchor = utcNow.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) + "||";

        public override async Task VisitAsync(GroupNode node, IQueryVisitorContext context)
        {
            // A custom field on a group is stored by the type of the group's terms, as the events index decides it. When that
            // is a date, name the stored date field so the frozen bounds below keep the date type.
            string? storedField = EventFieldsQueryVisitor.GetCustomFieldName(node.Field, EventFieldsQueryVisitor.GetGroupChildTerms(node));
            if (IsStoredDateField(storedField))
                node.Field = storedField;

            foreach (var child in node.Children)
                await child.AcceptAsync(this, context);
        }

        public override async Task VisitAsync(TermRangeNode node, IQueryVisitorContext context)
        {
            if (!IsRelative(node.Min) && !IsRelative(node.Max))
                return;

            if (!await EnsureDateFieldAsync(node, [node.Min, node.Max]))
                return;

            if (IsRelative(node.Min))
            {
                node.Min = _anchor + node.Min![3..];
                node.IsMinQuotedTerm = true;
            }

            if (IsRelative(node.Max))
            {
                node.Max = _anchor + node.Max![3..];
                node.IsMaxQuotedTerm = true;
            }
        }

        public override async Task VisitAsync(TermNode node, IQueryVisitorContext context)
        {
            if (node.IsRegexTerm || node.Proximity is not null || !IsRelative(node.Term) || node.Term!.IndexOfAny(['*', '?']) >= 0
                || !await EnsureDateFieldAsync(node, [node.Term]))
                return;

            // Elasticsearch treats a date term such as now/d as an inclusive date range, so it needs the same anchor.
            node.Term = _anchor + node.Term![3..];
            node.IsQuotedTerm = true;
        }

        private async ValueTask<bool> EnsureDateFieldAsync(IFieldQueryNode node, string?[] terms)
        {
            // An unfielded term or range inside a group takes its field from the group.
            string? field = node.Field ?? GetGroupField(node);
            if (String.IsNullOrEmpty(field))
                return false;

            if (node.Field is null)
            {
                // The group already decided the stored field: a custom field left unchanged there is not a date.
                if (!IsStoredDateField(field) && (IsCustomField(field) || !await isDateFieldAsync(field)))
                    return false;
            }
            else
            {
                // Custom data fields are stored by type. Freezing their original terms would hide the date type,
                // so name the stored date field directly.
                string? storedField = EventFieldsQueryVisitor.GetCustomFieldName(field, terms);
                if (IsCustomField(field))
                {
                    if (!IsStoredDateField(storedField))
                        return false;

                    node.Field = storedField;
                }
                else if (!IsStoredDateField(field) && !await isDateFieldAsync(field))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCustomField(string field)
        {
            return !String.Equals(EventFieldsQueryVisitor.GetCustomFieldName(field, []), field, StringComparison.Ordinal);
        }

        private static bool IsStoredDateField(string? field)
        {
            return field is not null && field.StartsWith("idx.", StringComparison.OrdinalIgnoreCase) && field.EndsWith("-d", StringComparison.Ordinal);
        }

        private static string? GetGroupField(IQueryNode node)
        {
            for (var parent = node.Parent; parent is not null; parent = parent.Parent)
            {
                if (parent is GroupNode { Field: { Length: > 0 } groupField })
                    return groupField;
            }

            return null;
        }

        private static bool IsRelative(string? value)
        {
            return value is not null && value.StartsWith("now", StringComparison.Ordinal) && (value.Length == 3 || value[3] is '+' or '-' or '/');
        }
    }
}
