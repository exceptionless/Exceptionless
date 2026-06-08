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
            ["events"] = new HashSet<string> { "summary", "user", "date", "message", "event_type", "type", "source", "name", "level" },
            ["stacks"] = new HashSet<string> { "summary", "status", "users", "events", "first", "last" },
            ["stream"] = new HashSet<string> { "summary", "user", "date", "message", "event_type", "type", "source", "name", "level" }
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
    [RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    public string? Slug { get; set; }

    [Required]
    public string ViewType { get; set; } = null!;

    [MaxLength(SavedView.MaxFilterDefinitionsLength)]
    public string? FilterDefinitions { get; set; }

    [MaxLength(50)]
    public Dictionary<string, bool>? Columns { get; set; }

    [MaxLength(50)]
    public List<string>? ColumnOrder { get; set; }

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

        foreach (var error in ValidateColumnKeys(ViewType, Columns))
        {
            yield return error;
        }

        foreach (var error in ValidateColumnOrder(ViewType, ColumnOrder))
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

        var validKeys = view is not null && ValidColumnIds.TryGetValue(view, out var viewKeys)
            ? viewKeys
            : AllValidColumnIds;

        var invalidKeys = columns.Keys.Where(key => !validKeys.Contains(key));
        foreach (var key in invalidKeys)
        {
            yield return new ValidationResult(
                $"Column key '{key}' is not a valid column. Valid columns are: {String.Join(", ", validKeys.Order())}.",
                [nameof(Columns)]
            );
        }
    }

    internal static IEnumerable<ValidationResult> ValidateColumnOrder(string? view, IReadOnlyCollection<string>? columnOrder)
    {
        if (columnOrder is null || columnOrder.Count == 0)
        {
            yield break;
        }

        var validKeys = view is not null && ValidColumnIds.TryGetValue(view, out var viewKeys)
            ? viewKeys
            : AllValidColumnIds;

        var invalidKeys = columnOrder.Where(key => !validKeys.Contains(key)).Distinct();
        foreach (var key in invalidKeys)
        {
            yield return new ValidationResult(
                $"Column order key '{key}' is not a valid column. Valid columns are: {String.Join(", ", validKeys.Order())}.",
                [nameof(ColumnOrder)]
            );
        }

        var duplicateKeys = columnOrder.GroupBy(key => key).Where(group => group.Count() > 1).Select(group => group.Key);
        foreach (var key in duplicateKeys)
        {
            yield return new ValidationResult(
                $"Column order key '{key}' cannot be repeated.",
                [nameof(ColumnOrder)]
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
