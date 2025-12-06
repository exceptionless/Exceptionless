using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Foundatio.Repositories.Models;
using Newtonsoft.Json.Converters;

namespace Exceptionless.Core.Models;

[DebuggerDisplay("Id={Id} Type={Type} Status={Status} IsDeleted={IsDeleted} Title={Title} TotalOccurrences={TotalOccurrences}")]
public class Stack : IOwnedByOrganizationAndProjectWithIdentity, IHaveDates, ISupportSoftDeletes
{
    /// <summary>
    /// Unique id that identifies a stack.
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// The organization that the stack belongs to.
    /// </summary>
    public string OrganizationId { get; set; } = null!;

    /// <summary>
    /// The project that the stack belongs to.
    /// </summary>
    public string ProjectId { get; set; } = null!;

    /// <summary>
    /// The stack type (ie. error, log message, feature usage). Check <see cref="KnownTypes">Stack.KnownTypes</see> for standard stack types.
    /// </summary>
    public string Type { get; set; } = null!;

    /// <summary>
    /// The stack status (ie. open, fixed, regressed,
    /// </summary>
    public StackStatus Status { get; set; } = StackStatus.Open;

    /// <summary>
    /// The date that the stack should be snoozed until.
    /// </summary>
    public DateTime? SnoozeUntilUtc { get; set; }

    /// <summary>
    /// The signature used for stacking future occurrences.
    /// </summary>
    public string SignatureHash { get; set; } = null!;

    /// <summary>
    /// The collection of information that went into creating the signature hash for the stack.
    /// </summary>
    public SettingsDictionary SignatureInfo { get; set; } = new();

    /// <summary>
    /// The version the stack was fixed in.
    /// </summary>
    public string? FixedInVersion { get; set; }

    /// <summary>
    /// The date the stack was fixed.
    /// </summary>
    public DateTime? DateFixed { get; set; }

    /// <summary>
    /// The stack title.
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// The total number of occurrences in the stack.
    /// </summary>
    public int TotalOccurrences { get; set; }

    /// <summary>
    /// The date of the 1st occurrence of this stack in UTC time.
    /// </summary>
    public DateTime FirstOccurrence { get; set; }

    /// <summary>
    /// The date of the last occurrence of this stack in UTC time.
    /// </summary>
    public DateTime LastOccurrence { get; set; }

    /// <summary>
    /// The stack description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// If true, all future occurrences will be marked as critical.
    /// </summary>
    public bool OccurrencesAreCritical { get; set; }

    /// <summary>
    /// A list of references.
    /// </summary>
    public ICollection<string> References { get; set; } = new Collection<string>();

    /// <summary>
    /// A list of tags used to categorize this stack.
    /// </summary>
    public TagSet Tags { get; set; } = new();

    /// <summary>
    /// The signature used for finding duplicate stacks. (ProjectId, SignatureHash)
    /// </summary>
    public string DuplicateSignature { get; set; } = null!;

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public bool IsDeleted { get; set; }

    public bool AllowNotifications => Status != StackStatus.Fixed && Status != StackStatus.Ignored && Status != StackStatus.Discarded && Status != StackStatus.Snoozed;

    public static class KnownTypes
    {
        public const string Error = "error";
        public const string FeatureUsage = "usage";
        public const string SessionHeartbeat = "heartbeat";
        public const string Log = "log";
        public const string NotFound = "404";
        public const string Session = "session";
        public const string SessionEnd = "sessionend";
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum StackStatus
{
    [JsonStringEnumMemberName("open")]
    [EnumMember(Value = "open")]
    Open,
    [JsonStringEnumMemberName("fixed")]
    [EnumMember(Value = "fixed")]
    Fixed,
    [JsonStringEnumMemberName("regressed")]
    [EnumMember(Value = "regressed")]
    Regressed,
    [JsonStringEnumMemberName("snoozed")]
    [EnumMember(Value = "snoozed")]
    Snoozed,
    [JsonStringEnumMemberName("ignored")]
    [EnumMember(Value = "ignored")]
    Ignored,
    [JsonStringEnumMemberName("discarded")]
    [EnumMember(Value = "discarded")]
    Discarded
}
