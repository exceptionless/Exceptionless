namespace Exceptionless.Web.Assistant;

internal static class AssistantRoutes
{
    public static string ProjectConfigure(string projectId) => $"/next/project/{Uri.EscapeDataString(projectId)}/configure";
}
