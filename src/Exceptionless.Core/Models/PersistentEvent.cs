using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Exceptionless.Core.Attributes;
using Exceptionless.Core.Extensions;
using Foundatio.Repositories.Elasticsearch.CustomFields;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

[DebuggerDisplay("Id: {Id}, Type: {Type}, Date: {Date}, Message: {Message}, Value: {Value}, Count: {Count}")]
public class PersistentEvent : Event, IOwnedByOrganizationAndProjectAndStackWithIdentity, IHaveCreatedDate, IValidatableObject, IHaveVirtualCustomFields
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
    [MiniValidation.SkipRecursion]
    public DataDictionary? Idx { get; set; }

    // IHaveVirtualCustomFields explicit implementation
    IDictionary<string, object> IHaveVirtualCustomFields.Idx => (IDictionary<string, object>)(Idx ??= new DataDictionary());

    public string GetTenantKey() => OrganizationId;

    public IDictionary<string, object?> GetCustomFields()
    {
        if (Data is null) return new DataDictionary();
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in Data.Where(kvp => !String.IsNullOrEmpty(kvp.Key)
            && (!kvp.Key.StartsWith('@') || kvp.Key.StartsWith("@ref:", StringComparison.OrdinalIgnoreCase))))
        {
            if (kvp.Value is string or bool or int or long or float or double or decimal or DateTime or DateTimeOffset)
                result[kvp.Key] = kvp.Value;
        }
        return result;
    }

    public object GetCustomField(string name) => Data is not null && Data.TryGetValue(name, out var v) && v is not null ? v : null!;
    public void SetCustomField(string name, object value) { Data ??= new DataDictionary(); Data[name] = value; }
    public void RemoveCustomField(string name) => Data?.Remove(name);

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

        // NOTE: We need to write a migration to cleanup all old events of 50 or more tags so there never is an error while saving.
        //if (ev.Tags.Count > 50)
        //    yield return new ValidationResult("Tags can't include more than 50 tags.", nameof(Tags));

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
