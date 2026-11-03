using Foundatio.Parsers.LuceneQueries;
using Foundatio.Parsers.LuceneQueries.Nodes;
using Foundatio.Parsers.LuceneQueries.Visitors;

namespace Exceptionless.Core.Services.BulkOperations;

/// <summary>
/// Pins relative dates in a filter (for example <c>data.date:[now/d-4d TO now/d+1d}</c>) to a fixed time, so a bulk
/// operation keeps selecting the same events no matter when each batch runs.
/// </summary>
public static class RelativeDateFreezer
{
    public static async Task<string?> FreezeAsync(string? filter, DateTime utcNow)
    {
        if (String.IsNullOrWhiteSpace(filter) || filter.IndexOf("now", StringComparison.OrdinalIgnoreCase) < 0)
            return filter;

        var result = await new LuceneQueryParser().ParseAsync(filter);
        if (result is null)
            return filter;

        await new FreezeRelativeDatesVisitor(utcNow).AcceptAsync(result, new QueryVisitorContext());
        return result.ToString();
    }

    private sealed class FreezeRelativeDatesVisitor(DateTime utcNow) : ChainableQueryVisitor
    {
        // Elasticsearch date math anchors an expression on a date with ||, for example 2026-11-03T04:00:00.000Z||/d-4d.
        private readonly string _anchor = $"{utcNow.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.fffZ}||";

        public override Task VisitAsync(TermRangeNode node, IQueryVisitorContext context)
        {
            if (TryFreeze(node.Min, out string? min))
            {
                node.Min = min;
                node.IsMinQuotedTerm = true;
            }

            if (TryFreeze(node.Max, out string? max))
            {
                node.Max = max;
                node.IsMaxQuotedTerm = true;
            }

            return Task.CompletedTask;
        }

        private bool TryFreeze(string? value, out string? frozen)
        {
            frozen = null;
            if (value is null || !value.StartsWith("now", StringComparison.OrdinalIgnoreCase))
                return false;

            string math = value[3..];
            if (math.Length > 0 && math[0] is not ('+' or '-' or '/'))
                return false;

            frozen = _anchor + math;
            return true;
        }
    }
}
