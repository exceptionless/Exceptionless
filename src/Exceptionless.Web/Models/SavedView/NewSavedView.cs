using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Exceptionless.Core.Attributes;
using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public record NewSavedView : IOwnedByOrganization, IValidatableObject
{
    /// <summary>The set of valid dashboard view identifiers.</summary>
    public static readonly string[] ValidViews = ["events", "issues", "stream"];

    /// <summary>Valid column IDs per view, matching the TanStack Table column definitions.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ValidColumnIds =
        new Dictionary<string, IReadOnlySet<string>>
        {
            ["events"] = new HashSet<string> { "user", "date" },
            ["issues"] = new HashSet<string> { "status", "users", "events", "first", "last" },
            ["stream"] = new HashSet<string> { "user", "date" }
        };

    /// <summary>Union of all valid column IDs across all views.</summary>
    public static readonly IReadOnlySet<string> AllValidColumnIds =
        new HashSet<string>(ValidColumnIds.Values.SelectMany(ids => ids));

    public static readonly string ValidViewsPattern = $"^({String.Join("|", ValidViews)})$";

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

    /// <summary>If true, this view will be the default for its view type. Defaults to false.</summary>
    public bool? IsDefault { get; set; }

    /// <summary>If true, the view will only be visible to the current user. Defaults to false.</summary>
    public bool? IsPrivate { get; set; }

    /// <summary>Set by the controller based on <see cref="IsPrivate"/>. Not deserialized from the request body.</summary>
    [JsonIgnore]
    public string? UserId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (View is { Length: > 0 } && !ValidViews.Contains(View))
        {
            yield return new ValidationResult(
                $"View must be one of: {String.Join(", ", ValidViews)}",
                [nameof(View)]
            );
        }

        if (FilterDefinitions is { Length: > 0 } && !IsValidJsonArray(FilterDefinitions))
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
