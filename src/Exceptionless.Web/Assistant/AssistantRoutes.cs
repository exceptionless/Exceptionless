namespace Exceptionless.Web.Assistant;

internal static class AssistantRoutes
{
    public static string Event(string stackId, string eventId)
        => $"/next/stack/{Uri.EscapeDataString(stackId)}/event/{Uri.EscapeDataString(eventId)}";

    public static string ProjectConfigure(string projectId) => $"/next/project/{Uri.EscapeDataString(projectId)}/configure";

    public static string ProjectStacks(string projectId) => $"/next/project/{Uri.EscapeDataString(projectId)}/stacks";

    public static string Stack(string stackId) => $"/next/stack/{Uri.EscapeDataString(stackId)}";
}
