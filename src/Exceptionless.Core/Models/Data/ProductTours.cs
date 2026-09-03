using System.Collections.Frozen;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Exceptionless.Core.Models.Data;

public static class ProductTours
{
    public const string StepTagPrefix = "product-tour-step:";

    public static FrozenDictionary<string, string[]> Steps { get; } = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [AppOverview] = ["navigation", "command-search", "saved-views", "exie", "help"],
        [EventInvestigate] = ["filter-errors", "choose-error", "stack-summary", "stack-triage", "event-occurrence", "tab-overview", "filter-stack-events"],
        [ExieOverview] = ["open-exie", "exie-context"],
        [ProjectConfigure] = ["organization-name", "project-name", "choose-platform", "sdk-instructions", "wait-for-event", "event-received"],
        [SavedViewCreate] = ["open-view-menu", "review-settings", "name-view", "private-view", "save-view", "view-created"]
    }.ToFrozenDictionary(StringComparer.Ordinal);
    public const string AppOverview = "app-overview";
    public const string AppWelcome = "app-welcome";
    public const string ExieAnnouncement = "exie-announcement";
    public const string ExieOverview = "exie-overview";
    public const string EventInvestigate = "event-investigate";
    public const string ProjectConfigure = "project-configure";
    public const string SavedViewCreate = "saved-view-create";

    public static FrozenDictionary<string, ProductTourDefinition> Definitions { get; } = new[]
    {
        new ProductTourDefinition(AppOverview, 1, ProductTourKind.Guide),
        new ProductTourDefinition(AppWelcome, 1, ProductTourKind.Prompt),
        new ProductTourDefinition(ExieAnnouncement, 1, ProductTourKind.Prompt),
        new ProductTourDefinition(ExieOverview, 1, ProductTourKind.Guide),
        new ProductTourDefinition(EventInvestigate, 1, ProductTourKind.Guide),
        new ProductTourDefinition(ProjectConfigure, 1, ProductTourKind.Guide),
        new ProductTourDefinition(SavedViewCreate, 1, ProductTourKind.Guide)
    }.ToFrozenDictionary(definition => definition.Name, StringComparer.Ordinal);

    public static bool IsKnown(string name) => Definitions.ContainsKey(name);

    public static bool IsValid(string name, int version)
    {
        return Definitions.TryGetValue(name, out var definition) && version > 0 && version <= definition.CurrentVersion;
    }

    public static string CreateTelemetrySource(
        ProductTourTelemetryEvent telemetryEvent,
        string tourName,
        int version,
        ProductTourLaunchSource launchSource)
    {
        return $"product-tour.{GetTelemetryName(telemetryEvent)}.{tourName}.v{version}.{GetLaunchSourceName(launchSource)}";
    }

    private static string GetTelemetryName(ProductTourTelemetryEvent telemetryEvent) => telemetryEvent switch
    {
        ProductTourTelemetryEvent.Completed => "completed",
        ProductTourTelemetryEvent.Dismissed => "dismissed",
        ProductTourTelemetryEvent.Shown => "shown",
        ProductTourTelemetryEvent.Started => "started",
        ProductTourTelemetryEvent.StepReached => "step-reached",
        _ => throw new ArgumentOutOfRangeException(nameof(telemetryEvent), telemetryEvent, "Unknown product tour telemetry event.")
    };

    private static string GetLaunchSourceName(ProductTourLaunchSource launchSource) => launchSource switch
    {
        ProductTourLaunchSource.Welcome => "welcome",
        ProductTourLaunchSource.Catalog => "catalog",
        ProductTourLaunchSource.CommandPalette => "command-palette",
        ProductTourLaunchSource.FeatureAnnouncement => "feature-announcement",
        ProductTourLaunchSource.HelpMenu => "help-menu",
        _ => throw new ArgumentOutOfRangeException(nameof(launchSource), launchSource, "Unknown product tour launch source.")
    };
}

public sealed record ProductTourDefinition(string Name, int CurrentVersion, ProductTourKind Kind);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProductTourKind
{
    [JsonStringEnumMemberName("guide")]
    [EnumMember(Value = "guide")]
    Guide,
    [JsonStringEnumMemberName("prompt")]
    [EnumMember(Value = "prompt")]
    Prompt
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
    Started,
    [JsonStringEnumMemberName("step-reached")]
    [EnumMember(Value = "step-reached")]
    StepReached
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProductTourLaunchSource
{
    [JsonStringEnumMemberName("welcome")]
    [EnumMember(Value = "welcome")]
    Welcome,
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
