using Exceptionless.Core.Extensions;
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
        var validatableArguments = context.Arguments
            .Where(arg => arg is not null && ShouldValidate(arg.GetType()));

        foreach (var argument in validatableArguments)
        {
            if (!MiniValidator.TryValidate(argument!, out var errors))
            {
                var normalizedErrors = errors.ToDictionary(
                    e => e.Key.ToLowerUnderscoredWords(),
                    e => e.Value);

                return Microsoft.AspNetCore.Http.Results.ValidationProblem(normalizedErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }
        }

        return await next(context);
    }

    private static bool ShouldValidate(Type type) =>
        !type.IsPrimitive
        && type != typeof(string)
        && !type.IsValueType
        && type.Namespace?.StartsWith("Microsoft.") != true
        && type.Namespace?.StartsWith("System.") != true;
}
