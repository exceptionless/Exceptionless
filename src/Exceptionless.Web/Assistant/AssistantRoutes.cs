namespace Exceptionless.Web.Assistant;

internal static class AssistantRoutes
{
    public static string Event(string stackId, string eventId)
        => $"/stack/{Uri.EscapeDataString(stackId)}/event/{Uri.EscapeDataString(eventId)}";

    public static string ProjectConfigure(string projectId) => $"/project/{Uri.EscapeDataString(projectId)}/configure";

    public static string ProjectStacks(string projectId) => $"/project/{Uri.EscapeDataString(projectId)}/stacks";

    public static string Stack(string stackId) => $"/stack/{Uri.EscapeDataString(stackId)}";

    /// <summary>Matches savedViewHref in the app: a stream view loads by id, and the other pages load by slug.</summary>
    public static string SavedView(string viewType, string slug, string id) => viewType switch
    {
        "events" => $"/event/{Uri.EscapeDataString(slug)}",
        "sessions" => $"/sessions/{Uri.EscapeDataString(slug)}",
        "stream" => $"/stream?saved={Uri.EscapeDataString(id)}",
        _ => $"/stack/{Uri.EscapeDataString(slug)}"
    };
}
