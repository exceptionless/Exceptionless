using MiniValidation;

namespace Exceptionless.Web.Api.Filters;

/// <summary>
/// Endpoint filter that automatically validates all parameters with DataAnnotation attributes
/// using MiniValidation, equivalent to the old AutoValidationActionFilter for MVC controllers.
/// </summary>
public class AutoValidationEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is null)
                continue;

            var argumentType = argument.GetType();

            // Skip primitives, strings, value types, and framework types
            if (argumentType.IsPrimitive || argumentType == typeof(string) || argumentType.IsValueType)
                continue;
            if (argumentType.Namespace?.StartsWith("Microsoft.") == true || argumentType.Namespace?.StartsWith("System.") == true)
                continue;

            if (!MiniValidator.TryValidate(argument, out var errors))
            {
                return Microsoft.AspNetCore.Http.Results.ValidationProblem(errors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        }

        return await next(context);
    }
}
