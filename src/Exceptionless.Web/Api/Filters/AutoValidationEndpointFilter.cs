using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Exceptionless.Core.Extensions;
using MiniValidation;

namespace Exceptionless.Web.Api.Filters;

/// <summary>
/// Endpoint filter that automatically validates all parameters with DataAnnotation attributes
/// using MiniValidation, equivalent to the old AutoValidationActionFilter for MVC controllers.
/// </summary>
public class AutoValidationEndpointFilter : IEndpointFilter
{
    private static readonly ConcurrentDictionary<Type, bool> s_validationCandidateCache = new();

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
        s_validationCandidateCache.GetOrAdd(type, static t =>
            !t.IsPrimitive
            && t != typeof(string)
            && !t.IsValueType
            && !t.IsInterface
            && !t.IsAbstract
            && t.Namespace?.StartsWith("Microsoft.") != true
            && t.Namespace?.StartsWith("System.") != true
            && HasValidationMetadata(t));

    private static bool HasValidationMetadata(Type type)
    {
        if (typeof(IValidatableObject).IsAssignableFrom(type))
            return true;

        if (type.GetCustomAttributes<ValidationAttribute>(inherit: true).Any())
            return true;

        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Any(property => property.GetCustomAttributes<ValidationAttribute>(inherit: true).Any());
    }
}
