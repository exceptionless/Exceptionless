using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Core.Models;

/// <summary>Configuration of a saved view's single event chart. Null retains the default event-count chart.</summary>
public sealed record EventChart : IValidatableObject
{
    [RegularExpression(EventMeasurement.NamePattern)]
    public string? Measurement { get; set; }

    [StringLength(32, MinimumLength = 1)]
    public string? Unit { get; set; }

    [Required, RegularExpression("^(avg|min|max|sum|p50|p95|p99|count)$")]
    public string Aggregation { get; set; } = "avg";

    [RegularExpression("^(source|stack|outcome|result|labels\\.[a-zA-Z][a-zA-Z0-9_.-]{0,99})$")]
    public string? GroupBy { get; set; }

    [Required, RegularExpression("^(buckets|events)$")]
    public string Mode { get; set; } = "buckets";

    [Required, RegularExpression("^(line|bar)$")]
    public string Display { get; set; } = "line";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Measurement is null && (Aggregation != "count" || Mode == "events"))
            yield return new ValidationResult("Select a measurement, or use bucketed event counts.", [nameof(Measurement)]);
        if ((Measurement is null) != (Unit is null))
            yield return new ValidationResult("A measurement and its unit must be selected together.", [nameof(Unit)]);
    }
}
