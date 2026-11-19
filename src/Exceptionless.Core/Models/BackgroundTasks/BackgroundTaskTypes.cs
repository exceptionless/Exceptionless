namespace Exceptionless.Core.Models.BackgroundTasks;

/// <summary>The types of tasks that are not bulk actions. Bulk actions use the key of their handler, e.g. <c>events.delete</c>.</summary>
public static class BackgroundTaskTypes
{
    public const string ResetProjectData = "projects.reset-data";
    public const string GenerateProjectSampleData = "projects.sample-data";
}
