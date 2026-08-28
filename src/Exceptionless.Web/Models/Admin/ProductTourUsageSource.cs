using System.Globalization;
using System.Text.RegularExpressions;

namespace Exceptionless.Web.Models.Admin;

internal sealed partial record ProductTourUsageSource(string Raw, string Event, string TourName, int Version, string LaunchSource)
{
    public const string Prefix = "product-tour.";
    public const string CompletedEvent = "completed";
    public const string DismissedEvent = "dismissed";
    public const string ShownEvent = "shown";
    public const string StartedEvent = "started";

    private static readonly ISet<string> _events = new HashSet<string>(
        [CompletedEvent, DismissedEvent, ShownEvent, StartedEvent],
        StringComparer.Ordinal);
    private static readonly ISet<string> _launchSources = new HashSet<string>(
        ["automatic", "catalog", "command-palette", "feature-announcement", "help-menu"],
        StringComparer.Ordinal);

    public static bool TryParse(string? value, out ProductTourUsageSource source)
    {
        source = default!;
        if (String.IsNullOrWhiteSpace(value))
            return false;

        var match = SourceRegex().Match(value);
        string eventName = match.Groups["event"].Value;
        string launchSource = match.Groups["launchSource"].Value;
        if (!match.Success
            || !_events.Contains(eventName)
            || !_launchSources.Contains(launchSource)
            || !Int32.TryParse(match.Groups["version"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out int version))
        {
            return false;
        }

        source = new ProductTourUsageSource(value, eventName, match.Groups["tourName"].Value, version, launchSource);
        return true;
    }

    [GeneratedRegex(@"^product-tour\.(?<event>[a-z-]+)\.(?<tourName>[a-z0-9]+(?:-[a-z0-9]+)*)\.v(?<version>[1-9][0-9]*)\.(?<launchSource>[a-z-]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceRegex();
}
