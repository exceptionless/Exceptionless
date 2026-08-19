using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Core.Attributes;
using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public record NewSavedView : IOwnedByOrganization, IValidatableObject
{
    /// <summary>The set of valid dashboard view type identifiers.</summary>
    public static readonly string[] ValidViewTypes = ["events", "stacks", "stream"];

    /// <summary>Valid column IDs per view, matching the TanStack Table column definitions.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ValidColumnIds =
        new Dictionary<string, IReadOnlySet<string>>
        {
            ["events"] = new HashSet<string> { "summary", "user", "date", "project", "tags", "message", "type", "version", "exception_type", "source", "name", "level" },
            ["stacks"] = new HashSet<string> { "summary", "project", "tags", "status", "users", "events", "first", "last" },
            ["stream"] = new HashSet<string> { "summary", "user", "date", "project", "tags", "message", "type", "version", "exception_type", "source", "name", "level" }
        };

    /// <summary>Union of all valid column IDs across all views.</summary>
    public static readonly IReadOnlySet<string> AllValidColumnIds =
        new HashSet<string>(ValidColumnIds.Values.SelectMany(ids => ids));

    public static readonly string ValidViewTypesPattern = $"^({String.Join("|", ValidViewTypes)})$";

    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [MaxLength(2000)]
    public string? Filter { get; set; }

    [MaxLength(100)]
    public string? Time { get; set; }

    [MaxLength(100)]
    public string? Sort { get; set; }

    [MaxLength(100)]
    [RegularExpression(SavedView.SlugPattern)]
    public string? Slug { get; set; }

    [Required]
    public string ViewType { get; set; } = null!;

    [MaxLength(SavedView.MaxFilterDefinitionsLength)]
    public string? FilterDefinitions { get; set; }

    [MaxLength(50)]
    public Dictionary<string, SavedViewColumnSettings>? Columns { get; set; }

    public bool? ShowStats { get; set; }

    public bool? ShowChart { get; set; }

    /// <summary>If true, the view will only be visible to the current user. Defaults to false.</summary>
    public bool? IsPrivate { get; set; }

    /// <summary>Set by the controller based on <see cref="IsPrivate"/>. Not deserialized from the request body.</summary>
    [JsonIgnore]
    public string? UserId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ViewType is { Length: > 0 } && !ValidViewTypes.Contains(ViewType))
        {
            yield return new ValidationResult(
                $"View type must be one of: {String.Join(", ", ValidViewTypes)}",
                [nameof(ViewType)]
            );
        }

        if (FilterDefinitions is { Length: > 0 } && !IsValidJsonArray(FilterDefinitions))
        {
            yield return new ValidationResult(
                "FilterDefinitions must be a valid JSON array",
                [nameof(FilterDefinitions)]
            );
        }

        foreach (var error in ValidateColumns(ViewType, Columns))
        {
            yield return error;
        }
    }

    internal static IEnumerable<ValidationResult> ValidateColumns(
        string? view,
        IReadOnlyDictionary<string, SavedViewColumnSettings>? columns)
    {
        if (columns is null || columns.Count == 0)
        {
            yield break;
        }

        if (columns.Count > 50)
        {
            yield return new ValidationResult(
                "Columns cannot exceed 50 items.",
                [nameof(Columns)]
            );
        }

        var validKeys = view is not null && ValidColumnIds.TryGetValue(view, out var viewKeys)
            ? viewKeys
            : AllValidColumnIds;

        foreach (string key in columns.Keys.Where(key => !validKeys.Contains(key)))
        {
            yield return new ValidationResult(
                $"Column key '{key}' is not a valid column. Valid columns are: {String.Join(", ", validKeys.Order())}.",
                [nameof(Columns)]
            );
        }

        foreach (var (key, settings) in columns)
        {
            if (settings is null)
            {
                yield return new ValidationResult(
                    $"Column configuration for '{key}' cannot be null.",
                    [nameof(Columns)]
                );
                continue;
            }

            if (settings.Position is < 0 or > SavedViewColumnSettings.MaxPosition)
            {
                yield return new ValidationResult(
                    $"Column position for '{key}' must be between 0 and {SavedViewColumnSettings.MaxPosition}.",
                    [nameof(Columns)]
                );
            }

            if (settings.Width is < SavedViewColumnSettings.MinWidth or > SavedViewColumnSettings.MaxWidth)
            {
                yield return new ValidationResult(
                    $"Column width for '{key}' must be between {SavedViewColumnSettings.MinWidth} and {SavedViewColumnSettings.MaxWidth} pixels.",
                    [nameof(Columns)]
                );
            }

            if (settings.AutoFill == true && settings.Width is not null)
            {
                yield return new ValidationResult(
                    $"Auto-fill column '{key}' cannot also have a fixed width.",
                    [nameof(Columns)]
                );
            }

            if (settings.AutoFill == true && settings.Visible == false)
            {
                yield return new ValidationResult(
                    $"Auto-fill column '{key}' cannot be hidden.",
                    [nameof(Columns)]
                );
            }
        }

        if (columns.Count(entry => entry.Value?.AutoFill == true) > 1)
        {
            yield return new ValidationResult(
                "Only one column can auto-fill the remaining table width.",
                [nameof(Columns)]
            );
        }

        foreach (var duplicatePosition in columns
            .Where(entry => entry.Value is { Position: not null })
            .GroupBy(entry => entry.Value.Position!.Value)
            .Where(group => group.Count() > 1))
        {
            yield return new ValidationResult(
                $"Column position '{duplicatePosition.Key}' cannot be repeated.",
                [nameof(Columns)]
            );
        }
    }

    internal static bool IsValidJsonArray(string json)
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
