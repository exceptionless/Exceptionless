using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Exceptionless.Core.Attributes;

namespace Exceptionless.Web.Models;

public sealed record UpdateSavedViewOrder : IValidatableObject
{
    [Required]
    [MaxLength(100)]
    public required IReadOnlyList<string> SavedViewIds { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SavedViewIds.Count != SavedViewIds.Distinct(StringComparer.Ordinal).Count())
        {
            yield return new ValidationResult(
                "Saved view identifiers cannot be repeated.",
                [nameof(SavedViewIds)]
            );
        }

        if (SavedViewIds.Any(id => id is null || !Regex.IsMatch(id, ObjectIdAttribute.ObjectIdPattern)))
        {
            yield return new ValidationResult(
                "Every saved view identifier must be a valid object identifier.",
                [nameof(SavedViewIds)]
            );
        }
    }
}
