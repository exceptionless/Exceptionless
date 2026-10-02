using System.Text.RegularExpressions;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Exceptionless.Core.Models;

namespace Exceptionless.Core.Repositories.Queries;

/// <summary>Resolves measurement.duration@ms to a value and its nested name/unit scope.</summary>
public static class MeasurementField
{
    public static bool TryParse(string field, out string name, out string unit)
    {
        name = unit = String.Empty;
        const string prefix = "measurement.";
        if (!field.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        int separator = field.IndexOf('@');
        if (separator <= prefix.Length || separator == field.Length - 1)
            return false;
        name = field[prefix.Length..separator];
        unit = field[(separator + 1)..];
        return unit.Length <= 32 && Regex.IsMatch(name, EventMeasurement.NamePattern);
    }

    public static Query? Filter(string originalField)
    {
        if (!TryParse(originalField, out string name, out string unit))
            return null;
        Query filter = new TermQuery { Field = "measurements.name", Value = name };
        return filter & new TermQuery { Field = "measurements.unit", Value = unit };
    }
}
