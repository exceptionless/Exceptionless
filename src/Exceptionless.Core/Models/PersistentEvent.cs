using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Exceptionless.Core.Attributes;
using Exceptionless.Core.Extensions;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

[DebuggerDisplay("Id: {Id}, Type: {Type}, Date: {Date}, Message: {Message}, Value: {Value}, Count: {Count}")]
public class PersistentEvent : Event, IOwnedByOrganizationAndProjectAndStackWithIdentity, IHaveCreatedDate, IValidatableObject
{
    /// <summary>
    /// Unique id that identifies an event.
    /// </summary>
    [ObjectId]
    public string Id { get; set; } = null!;

    /// <summary>
    /// The organization that the event belongs to.
    /// </summary>
    [Required]
    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    /// <summary>
    /// The project that the event belongs to.
    /// </summary>
    [Required]
    [ObjectId]
    public string ProjectId { get; set; } = null!;

    /// <summary>
    /// The stack that the event belongs to.
    /// </summary>
    [Required]
    [ObjectId]
    public string StackId { get; set; } = null!;

    /// <summary>
    /// Whether the event resulted in the creation of a new stack.
    /// </summary>
    public bool IsFirstOccurrence { get; set; }

    /// <summary>
    /// The date that the event was created in the system.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Used to store primitive data type custom data values for searching the event.
    /// </summary>
    public DataDictionary? Idx { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Date == DateTimeOffset.MinValue)
        {
            yield return new ValidationResult("Date must be specified.", [nameof(Date)]);
        }

        var timeProvider = validationContext.GetService(typeof(TimeProvider)) as TimeProvider
            ?? throw new InvalidOperationException("TimeProvider is not registered.");
        if (Date.UtcDateTime > timeProvider.GetUtcNow().UtcDateTime.AddHours(1))
        {
            yield return new ValidationResult("Date cannot be in the future.", [nameof(Date)]);
        }

        if (!this.HasValidReferenceId())
        {
            yield return new ValidationResult("ReferenceId must contain between 8 and 100 alphanumeric or '-' characters.", [nameof(ReferenceId)]);
        }

        if (Tags is not null)
        {
            foreach (string? tag in Tags)
            {
                if (String.IsNullOrEmpty(tag))
                {
                    yield return new ValidationResult("Tags can't be empty.", [nameof(Tags)]);
                }
                else if (tag.Length > 255)
                {
                    yield return new ValidationResult("A tag cannot be longer than 255 characters.", [nameof(Tags)]);
                }
            }
        }
    }
}
