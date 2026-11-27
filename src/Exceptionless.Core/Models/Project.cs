using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Exceptionless.Core.Attributes;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Models;

[DebuggerDisplay("Id: {Id}, Name: {Name}, NextSummaryEndOfDayTicks: {NextSummaryEndOfDayTicks}")]
public class Project : IOwnedByOrganizationWithIdentity, IData, IHaveDates, ISupportSoftDeletes, IValidatableObject
{
    public Project()
    {
        Configuration = new ClientConfiguration();
        NotificationSettings = new Dictionary<string, NotificationSettings>();
        PromotedTabs = [];
        Environments = [];
        DeleteBotDataEnabled = false;
        Usage = new SortedSet<UsageInfo>(Comparer<UsageInfo>.Create((a, b) => a.Date.CompareTo(b.Date)));
        UsageHours = new SortedSet<UsageHourInfo>(Comparer<UsageHourInfo>.Create((a, b) => a.Date.CompareTo(b.Date)));
        Data = new DataDictionary();
    }

    /// <summary>
    /// Unique id that identifies an project.
    /// </summary>
    [ObjectId]
    public string Id { get; set; } = null!;

    [Required]
    [ObjectId]
    public string OrganizationId { get; set; } = null!;

    [Required]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Returns true if we've detected that the project has received data.
    /// </summary>
    public bool? IsConfigured { get; set; }

    public ClientConfiguration Configuration { get; set; }

    public Dictionary<string, NotificationSettings> NotificationSettings { get; set; }

    /// <summary>
    /// Hourly project event usage information.
    /// </summary>
    public ICollection<UsageHourInfo> UsageHours { get; set; }

    /// <summary>
    /// Project event usage information.
    /// </summary>
    public ICollection<UsageInfo> Usage { get; set; }
    public DateTime? LastEventDateUtc { get; set; }

    /// <summary>
    /// Optional data entries that contain additional configuration information for this project.
    /// </summary>
    public DataDictionary? Data { get; set; }

    public List<string>? PromotedTabs { get; set; }

    public string CustomContent { get; set; } = null!;

    public bool DeleteBotDataEnabled { get; set; }

    /// <summary>
    /// The environments that this project lists. Names keep the casing that was entered. Events are never changed by adding or removing a name.
    /// </summary>
    public List<ProjectEnvironment> Environments { get; set; }

    /// <summary>
    /// If true, events whose environment is not listed are discarded at ingestion.
    /// </summary>
    public bool DiscardUnlistedEnvironments { get; set; }

    /// <summary>
    /// If true, events without an environment are also discarded. Only applies when <see cref="DiscardUnlistedEnvironments"/> is true.
    /// </summary>
    public bool DiscardEventsWithoutEnvironment { get; set; }

    /// <summary>
    /// If true, events from environments that are not listed, and events without an environment, can send notifications.
    /// </summary>
    public bool NotifyOtherEnvironments { get; set; } = true;

    /// <summary>
    /// The tick count that represents the next time the daily summary job should run. This time is set to midnight of the
    /// projects local time.
    /// </summary>
    public long NextSummaryEndOfDayTicks { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public bool IsDeleted { get; set; }

    public static class NotificationIntegrations
    {
        public const string Slack = "slack";
    }

    public static class KnownDataKeys
    {
        public const string SlackToken = "-@slack";
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NextSummaryEndOfDayTicks is 0)
        {
            yield return new ValidationResult("Please specify a valid next summary end of day ticks.",
                [nameof(NextSummaryEndOfDayTicks)]);
        }

        foreach (var result in ValidateEnvironments(Environments))
            yield return result;
    }

    public static IEnumerable<ValidationResult> ValidateEnvironments(IReadOnlyCollection<ProjectEnvironment>? environments)
    {
        if (environments is null)
            yield break;

        if (environments.Count > ProjectEnvironment.MaximumCount)
            yield return new ValidationResult($"A project cannot list more than {ProjectEnvironment.MaximumCount} environments.", [nameof(Environments)]);

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var environment in environments)
        {
            string? name = environment?.Name;
            if (name is null || !String.Equals(ProjectEnvironment.NormalizeName(name), name, StringComparison.Ordinal))
            {
                yield return new ValidationResult("Environment names must be 1 to 64 letters, digits, periods, underscores, or hyphens, and start with a letter or digit.", [nameof(Environments)]);
                continue;
            }

            if (!names.Add(ProjectEnvironment.GetKey(name)))
                yield return new ValidationResult($"The environment '{name}' is listed more than once. Names are case-insensitive.", [nameof(Environments)]);
        }
    }
}
