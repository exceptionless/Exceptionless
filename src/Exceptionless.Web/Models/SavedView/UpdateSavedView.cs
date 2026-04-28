using System.ComponentModel.DataAnnotations;

namespace Exceptionless.Web.Models;

public class UpdateSavedView : IValidatableObject
{
    [MaxLength(100)]
    public string? Name { get; set; }
    [MaxLength(2000)]
    public string? Filter { get; set; }
    [MaxLength(100)]
    public string? Time { get; set; }
    [MaxLength(10000)]
    public string? FilterDefinitions { get; set; }
    [MaxLength(50)]
    public Dictionary<string, bool>? Columns { get; set; }
    public bool? IsDefault { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FilterDefinitions is { Length: > 0 } && !NewSavedView.IsValidJsonArray(FilterDefinitions))
        {
            yield return new ValidationResult(
                "FilterDefinitions must be a valid JSON array",
                [nameof(FilterDefinitions)]
            );
        }

        foreach (var error in NewSavedView.ValidateColumnKeys(null, Columns))
        {
            yield return error;
        }
    }
}
