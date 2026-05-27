using Microsoft.AspNetCore.Http.HttpResults;
using MiniValidation;

namespace Exceptionless.Web.Api.Infrastructure;

public static class ApiValidation
{
    /// <summary>
    /// Validates an object using MiniValidation and returns a problem details result if invalid.
    /// </summary>
    public static async Task<IResult?> ValidateAsync<T>(T instance, IServiceProvider serviceProvider, int statusCode = StatusCodes.Status400BadRequest) where T : class
    {
        var (isValid, errors) = await MiniValidator.TryValidateAsync(instance, serviceProvider, recurse: true);
        if (isValid)
            return null;

        var problemErrors = new Dictionary<string, string[]>();
        foreach (var error in errors)
        {
            problemErrors[error.Key] = error.Value;
        }

        return global::Microsoft.AspNetCore.Http.Results.ValidationProblem(problemErrors, statusCode: statusCode);
    }

    /// <summary>
    /// Validates an object synchronously using MiniValidation.
    /// </summary>
    public static IResult? Validate<T>(T instance, int statusCode = StatusCodes.Status400BadRequest) where T : class
    {
        bool isValid = MiniValidator.TryValidate(instance, recurse: true, out var errors);
        if (isValid)
            return null;

        var problemErrors = new Dictionary<string, string[]>();
        foreach (var error in errors)
        {
            problemErrors[error.Key] = error.Value;
        }

        return global::Microsoft.AspNetCore.Http.Results.ValidationProblem(problemErrors, statusCode: statusCode);
    }
}
