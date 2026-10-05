using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Serialization;

namespace Exceptionless.Core.Validation;

public static class EventTelemetryValidation
{
    public static IEnumerable<ValidationResult> ValidateRelationshipsAndLabels(Event ev)
    {
        if (ev.ParentReferenceId is not null && (!EventTelemetryReader.IsReference(ev.ParentReferenceId) || ev.ParentReferenceId == ev.ReferenceId))
            yield return new ValidationResult("ParentReferenceId must be a valid reference to a different event.", [nameof(ev.ParentReferenceId)]);
        if (ev.RootReferenceId is not null && !EventTelemetryReader.IsReference(ev.RootReferenceId))
            yield return new ValidationResult("RootReferenceId must be a valid event reference.", [nameof(ev.RootReferenceId)]);
        if (ev.Measurements is not null && (ev.Measurements.Any(m => m is null) || ev.Measurements.Select(m => m?.Name).Distinct(StringComparer.Ordinal).Count() != ev.Measurements.Count))
            yield return new ValidationResult("Measurements must have unique names and cannot contain null entries.", [nameof(ev.Measurements)]);
        if (ev.Labels is not null && ev.Labels.Any(d => !Regex.IsMatch(d.Key, EventMeasurement.NamePattern) || d.Value is null || d.Value.Length > 256))
            yield return new ValidationResult("Label names must be valid measurement names and values must be strings of at most 256 characters.", [nameof(ev.Labels)]);
    }

    public static IDictionary<string, string[]> GetErrors(Event ev)
    {
        var results = ValidateRelationshipsAndLabels(ev).ToList();
        ValidateProperty(ev, nameof(ev.Outcome), ev.Outcome, results);
        ValidateProperty(ev, nameof(ev.Result), ev.Result, results);
        ValidateProperty(ev, nameof(ev.Measurements), ev.Measurements, results);
        ValidateProperty(ev, nameof(ev.Labels), ev.Labels, results);
        foreach (var measurement in ev.Measurements ?? [])
            if (measurement is not null)
                Validator.TryValidateObject(measurement, new ValidationContext(measurement), results, validateAllProperties: true);

        return results.SelectMany(result => result.MemberNames.DefaultIfEmpty("Measurements").Select(member => (member, result.ErrorMessage)))
            .GroupBy(result => result.member)
            .ToDictionary(group => group.Key, group => group.Select(result => result.ErrorMessage ?? "Invalid telemetry.").ToArray());
    }

    private static void ValidateProperty(Event ev, string name, object? value, List<ValidationResult> results)
        => Validator.TryValidateProperty(value, new ValidationContext(ev) { MemberName = name }, results);
}
