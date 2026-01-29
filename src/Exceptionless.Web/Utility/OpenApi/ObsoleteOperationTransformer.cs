using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Exceptionless.Web.Utility.OpenApi;

/// <summary>
/// Operation transformer that marks operations with [Obsolete] attribute as deprecated in OpenAPI.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> ASP.NET Core's built-in OpenAPI support does not automatically
/// set <c>deprecated: true</c> on operations marked with the <c>[Obsolete]</c> attribute.
/// Swashbuckle handled this automatically.
/// </para>
/// <para>
/// This transformer inspects the action method for <c>[Obsolete]</c> and sets the
/// <c>deprecated</c> flag on the OpenAPI operation accordingly.
/// </para>
/// </remarks>
public class ObsoleteOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var methodInfo = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<MethodInfo>()
            .FirstOrDefault();

        // For controller actions, try to get from ControllerActionDescriptor
        if (methodInfo is null &&
            context.Description.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor controllerDescriptor)
        {
            methodInfo = controllerDescriptor.MethodInfo;
        }

        if (methodInfo?.GetCustomAttribute<ObsoleteAttribute>() is not null)
        {
            operation.Deprecated = true;
        }

        return Task.CompletedTask;
    }
}
