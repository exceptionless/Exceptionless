using System.Collections.Frozen;

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

    public static bool IsKnown(string name) => Versions.ContainsKey(name);

    public static bool IsValid(string name, int version)
    {
        return Versions.TryGetValue(name, out int currentVersion) && version > 0 && version <= currentVersion;
    }
}
