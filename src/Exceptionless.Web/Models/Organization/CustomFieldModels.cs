using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Services;
using Foundatio.Repositories.Elasticsearch.CustomFields;

namespace Exceptionless.Web.Models;

public record NewCustomFieldDefinition : IValidatableObject
{
    [Required]
    [MaxLength(100)]
    public string Name { get; init; } = null!;

    [Required]
    [MaxLength(20)]
    public string IndexType { get; init; } = null!;

    [MaxLength(500)]
    public string? Description { get; init; }

    public int DisplayOrder { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!EventCustomFieldService.IsValidFieldName(Name))
            yield return new ValidationResult(
                "Field names must be 1-100 characters, alphanumeric with underscores, dots, and dashes, and cannot start with '@'.",
                [nameof(Name)]);

        if (!EventCustomFieldService.SupportedIndexTypes.Contains(IndexType))
            yield return new ValidationResult(
                $"Index type '{IndexType}' is not supported. Valid types are: {string.Join(", ", EventCustomFieldService.SupportedIndexTypes.Order())}.",
                [nameof(IndexType)]);
    }
}

public record UpdateCustomFieldDefinition
{
    [MaxLength(500)]
    public string? Description { get; init; }

    public int? DisplayOrder { get; init; }
}

/// <summary>
/// API response model for custom field definitions. Hides internal IndexSlot
/// to prevent users from directly querying raw slot names.
/// </summary>
public record CustomFieldDefinitionResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string IndexType { get; init; }
    public int DisplayOrder { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }

    public static CustomFieldDefinitionResponse FromDefinition(CustomFieldDefinition definition) => new()
    {
        Id = definition.Id,
        Name = definition.Name,
        Description = definition.Description,
        IndexType = definition.IndexType,
        DisplayOrder = definition.DisplayOrder,
        CreatedUtc = definition.CreatedUtc,
        UpdatedUtc = definition.UpdatedUtc
    };
}
