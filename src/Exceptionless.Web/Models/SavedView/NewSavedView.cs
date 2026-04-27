using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Core.Attributes;
using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public record NewSavedView : IOwnedByOrganization, IValidatableObject
{
    /// <summary>Regex pattern derived from <see cref="SavedView.ValidViews"/>.</summary>
    public static readonly string ValidViewsPattern = $"^({String.Join("|", SavedView.ValidViews)})$";

    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [MaxLength(2000)]
    public string? Filter { get; set; }

    [MaxLength(100)]
    public string? Time { get; set; }

    [Required]
    public string View { get; set; } = null!;

    [MaxLength(10000)]
    public string? FilterDefinitions { get; set; }

    [MaxLength(50)]
    public Dictionary<string, bool>? Columns { get; set; }

    public bool IsDefault { get; set; }

    /// <summary>Set by the controller when ?is_private=true. Not deserialized from the request body.</summary>
    [JsonIgnore]
    public string? UserId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!String.IsNullOrEmpty(View) && !SavedView.ValidViews.Contains(View))
        {
            yield return new ValidationResult(
                $"View must be one of: {String.Join(", ", SavedView.ValidViews)}",
                [nameof(View)]
            );
        }

        if (!String.IsNullOrEmpty(FilterDefinitions) && !IsValidJsonArray(FilterDefinitions))
        {
            yield return new ValidationResult(
                "FilterDefinitions must be a valid JSON array",
                [nameof(FilterDefinitions)]
            );
        }

        foreach (var error in ValidateColumnKeys(View, Columns))
        {
            yield return error;
        }
    }

    internal static IEnumerable<ValidationResult> ValidateColumnKeys(string? view, Dictionary<string, bool>? columns)
    {
        if (columns is null || columns.Count == 0)
        {
            yield break;
        }

        var validKeys = view is not null && SavedView.ValidColumnIds.TryGetValue(view, out var viewKeys)
            ? viewKeys
            : SavedView.AllValidColumnIds;

        var invalidKeys = columns.Keys.Where(key => !validKeys.Contains(key));
        foreach (var key in invalidKeys)
        {
            yield return new ValidationResult(
                $"Column key '{key}' is not a valid column. Valid columns are: {String.Join(", ", validKeys.Order())}.",
                [nameof(Columns)]
            );
        }
    }

    private static bool IsValidJsonArray(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
