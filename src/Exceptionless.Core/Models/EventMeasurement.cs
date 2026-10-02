using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Core.Models;

/// <summary>A numeric observation on one event. Units describe values, not their storage type.</summary>
public sealed record EventMeasurement : IValidatableObject
{
    public const string NamePattern = "^[a-zA-Z][a-zA-Z0-9_.-]{0,99}$";

    [Required, RegularExpression(NamePattern)]
    public string Name { get; set; } = null!;

    [Required]
    public double? Value { get; set; }

    /// <summary>Case-sensitive unit. Prefer UCUM/OTel symbols: s for duration, By for bytes, {call} for counts, or 1 for dimensionless values.</summary>
    [Required, StringLength(32, MinimumLength = 1)]
    public string Unit { get; set; } = null!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Value is { } value && !Double.IsFinite(value))
            yield return new ValidationResult("Measurement values must be finite.", [nameof(Value)]);
    }
}
