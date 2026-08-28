using System.Collections.Frozen;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Models.Data;

public static class ProductTours
{
    public const string ConfigureProject = "configure-project";
    public const string CreateSavedView = "create-saved-view";
    public const string ExieAnnouncement = "exie-announcement";
    public const string InvestigateError = "investigate-error";
    public const string MeetExie = "meet-exie";
    public const string UiOverview = "ui-overview";
    public const string Welcome = "welcome";

    public static IReadOnlyDictionary<string, int> Versions { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [ConfigureProject] = 1,
        [CreateSavedView] = 1,
        [ExieAnnouncement] = 1,
        [InvestigateError] = 1,
        [MeetExie] = 1,
        [UiOverview] = 1,
        [Welcome] = 1
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlyCollection<ProductTourTelemetryEvent> TelemetryEvents { get; } = Enum.GetValues<ProductTourTelemetryEvent>();
    public static IReadOnlyCollection<ProductTourLaunchSource> LaunchSources { get; } = Enum.GetValues<ProductTourLaunchSource>();

    public static bool IsKnown(string name) => Versions.ContainsKey(name);

    public static bool IsValid(string name, int version)
    {
        return Versions.TryGetValue(name, out int currentVersion) && version > 0 && version <= currentVersion;
    }

    public static string CreateTelemetrySource(
        ProductTourTelemetryEvent telemetryEvent,
        string tourName,
        int version,
        ProductTourLaunchSource launchSource)
    {
        return $"product-tour.{GetTelemetryName(telemetryEvent)}.{tourName}.v{version}.{GetLaunchSourceName(launchSource)}";
    }

    public static string GetTelemetryName(ProductTourTelemetryEvent telemetryEvent) => telemetryEvent.ToString().ToLowerUnderscoredWords('-');

    public static string GetLaunchSourceName(ProductTourLaunchSource launchSource) => launchSource.ToString().ToLowerUnderscoredWords('-');
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProductTourTelemetryEvent
{
    [JsonStringEnumMemberName("completed")]
    [EnumMember(Value = "completed")]
    Completed,
    [JsonStringEnumMemberName("dismissed")]
    [EnumMember(Value = "dismissed")]
    Dismissed,
    [JsonStringEnumMemberName("shown")]
    [EnumMember(Value = "shown")]
    Shown,
    [JsonStringEnumMemberName("started")]
    [EnumMember(Value = "started")]
    Started
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProductTourLaunchSource
{
    [JsonStringEnumMemberName("automatic")]
    [EnumMember(Value = "automatic")]
    Automatic,
    [JsonStringEnumMemberName("catalog")]
    [EnumMember(Value = "catalog")]
    Catalog,
    [JsonStringEnumMemberName("command-palette")]
    [EnumMember(Value = "command-palette")]
    CommandPalette,
    [JsonStringEnumMemberName("feature-announcement")]
    [EnumMember(Value = "feature-announcement")]
    FeatureAnnouncement,
    [JsonStringEnumMemberName("help-menu")]
    [EnumMember(Value = "help-menu")]
    HelpMenu
}
