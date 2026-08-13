using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Exceptionless.Core.Extensions;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using MiniValidation;

namespace Exceptionless.Web.Api.Filters;

/// <summary>
/// Endpoint filter that automatically validates all parameters with DataAnnotation attributes
/// using MiniValidation, equivalent to the old AutoValidationActionFilter for MVC controllers.
/// </summary>
public class AutoValidationEndpointFilter : IEndpointFilter
{
    private static readonly ConcurrentDictionary<ParameterInfo, ValidationAttribute[]> s_parameterValidationAttributesCache = new();
    private static readonly ConcurrentDictionary<Type, bool> s_validationCandidateCache = new();

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var isService = context.HttpContext.RequestServices as IServiceProviderIsService;
        var validatableArguments = context.Arguments
            .Where(arg => arg is not null && isService?.IsService(arg.GetType()) != true && ShouldValidate(arg.GetType()));
        var validationErrors = new List<KeyValuePair<string, string>>();

        ValidateParameters(context, validationErrors);

        foreach (var argument in validatableArguments)
        {
            var (isValid, errors) = await MiniValidator.TryValidateAsync(
                argument!,
                context.HttpContext.RequestServices,
                recurse: true);

            if (isValid)
                continue;

            validationErrors.AddRange(errors.SelectMany(error =>
                error.Value.Select(message => new KeyValuePair<string, string>(error.Key.ToLowerUnderscoredWords(), message))));
        }

        if (validationErrors.Count > 0)
        {
            var normalizedErrors = validationErrors
                .GroupBy(error => error.Key, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                    StringComparer.Ordinal);

            return Microsoft.AspNetCore.Http.Results.ValidationProblem(normalizedErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return await next(context);
    }

    private static void ValidateParameters(EndpointFilterInvocationContext context, List<KeyValuePair<string, string>> validationErrors)
    {
        var parameterMetadata = context.HttpContext.GetEndpoint()?.Metadata.GetOrderedMetadata<IParameterBindingMetadata>();
        if (parameterMetadata is null)
            return;

        foreach (var metadata in parameterMetadata)
        {
            var attributes = s_parameterValidationAttributesCache.GetOrAdd(metadata.ParameterInfo,
                static parameter => parameter.GetCustomAttributes<ValidationAttribute>(inherit: true).ToArray());
            if (attributes.Length == 0)
                continue;

            var parameterName = metadata.ParameterInfo.Name ?? metadata.Name;
            var value = context.Arguments[metadata.ParameterInfo.Position];
            var validationContext = new ValidationContext(value ?? new object(), context.HttpContext.RequestServices, items: null) {
                DisplayName = parameterName,
                MemberName = parameterName,
            };

            foreach (var attribute in attributes)
            {
                var result = attribute.GetValidationResult(value, validationContext);
                if (result == ValidationResult.Success)
                    continue;

                var memberNames = result?.MemberNames.Any() == true ? result.MemberNames : [parameterName];
                validationErrors.AddRange(memberNames.Select(name => new KeyValuePair<string, string>(name, result?.ErrorMessage ?? "The value is invalid.")));
            }
        }
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
            && (MiniValidator.RequiresValidation(t, recurse: true)
                || t.GetCustomAttributes(typeof(ValidationAttribute), inherit: true).Length > 0));
}
