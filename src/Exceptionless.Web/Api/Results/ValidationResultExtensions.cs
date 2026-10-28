using Exceptionless.Core.Extensions;
using Exceptionless.Core.Validation;
using Foundatio.Mediator;

namespace Exceptionless.Web.Api.Results;

public static class ValidationResultExtensions
{
    public static Result<T> ToValidationResult<T>(this MiniValidatorException exception)
    {
        return Result<T>.FromResult(Result.Invalid(exception.Errors.SelectMany(error =>
            error.Value.Select(message => ValidationError.Create(error.Key.ToLowerUnderscoredWords(), message)))));
    }
}
