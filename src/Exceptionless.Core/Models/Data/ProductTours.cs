using System.Collections.Frozen;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Models.Data;

public static class ProductTours
{
    public const string AppOverview = "app-overview";
    public const string AppWelcome = "app-welcome";
    public const string ExieAnnouncement = "exie-announcement";
    public const string ExieOverview = "exie-overview";
    public const string EventInvestigate = "event-investigate";
    public const string ProjectConfigure = "project-configure";
    public const string SavedViewCreate = "saved-view-create";

    public static IReadOnlyDictionary<string, int> Versions { get; } = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [AppOverview] = 1,
        [AppWelcome] = 1,
        [ExieAnnouncement] = 1,
        [ExieOverview] = 1,
        [EventInvestigate] = 1,
        [ProjectConfigure] = 1,
        [SavedViewCreate] = 1
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static bool IsKnown(string name) => Versions.ContainsKey(name);

    public static bool IsPrompt(string name) => name is AppWelcome or ExieAnnouncement;

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

    private static string GetTelemetryName(ProductTourTelemetryEvent telemetryEvent) => telemetryEvent.ToString().ToLowerUnderscoredWords('-');

    private static string GetLaunchSourceName(ProductTourLaunchSource launchSource) => launchSource.ToString().ToLowerUnderscoredWords('-');
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
