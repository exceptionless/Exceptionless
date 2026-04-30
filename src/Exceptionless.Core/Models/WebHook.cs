using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Attributes;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

public class WebHook : IOwnedByOrganizationAndProjectWithIdentity, IHaveCreatedDate, IValidatableObject
{
    [ObjectId]
    public string Id { get; set; } = null!;

    [Required]
    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    [ObjectId]
    public string ProjectId { get; set; } = null!;

    [Required]
    [Url]
    public string Url { get; set; } = null!;

    [Required]
    [Length(1, 6)]
    public string[] EventTypes { get; set; } = null!;

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The schema version that should be used.
    /// </summary>
    [Required]
    public string Version { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }

    public static class KnownVersions
    {
        public const string Version1 = "v1";
        public const string Version2 = "v2";
    }

    public static readonly string[] AllKnownEventTypes =
    [
        KnownEventTypes.NewError,
        KnownEventTypes.CriticalError,
        KnownEventTypes.NewEvent,
        KnownEventTypes.CriticalEvent,
        KnownEventTypes.StackRegression,
        KnownEventTypes.StackPromoted
    ];

    public static class KnownEventTypes
    {
        public const string NewError = "NewError";
        public const string CriticalError = "CriticalError";
        public const string NewEvent = "NewEvent";
        public const string CriticalEvent = "CriticalEvent";
        public const string StackRegression = "StackRegression";
        public const string StackPromoted = "StackPromoted";
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EventTypes is null)
            yield break;

        foreach (string eventType in EventTypes)
        {
            if (!AllKnownEventTypes.Contains(eventType))
                yield return new ValidationResult($"'{eventType}' is not a valid event type.", [nameof(EventTypes)]);
        }
    }
}
