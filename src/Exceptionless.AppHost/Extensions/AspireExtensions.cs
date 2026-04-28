using Aspire.Hosting.JavaScript;
using Microsoft.Extensions.Hosting;

public static class AspireExtensions
{
    // TODO: Remove this once the upstream Aspire bug that attaches a
    // SupportsDebuggingAnnotation to JavaScript resources (causing the dashboard
    // to surface a non-functional "Debug" command) is fixed.
    // See https://github.com/microsoft/aspire/issues/16468
    public static IResourceBuilder<TResource> RemoveJavaScriptDebuggingAnnotation<TResource>(this IResourceBuilder<TResource> resourceBuilder)
        where TResource : IResource
    {
        foreach (var annotation in resourceBuilder.Resource.Annotations
            .Where(a => a.GetType().Name == "SupportsDebuggingAnnotation")
            .ToArray())
        {
            resourceBuilder.Resource.Annotations.Remove(annotation);
        }

        return resourceBuilder;
    }
}
