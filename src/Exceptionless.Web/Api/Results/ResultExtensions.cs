using Exceptionless.Web.Controllers;
using Foundatio.Mediator;
using HttpResults = Microsoft.AspNetCore.Http.Results;
using IHttpResult = Microsoft.AspNetCore.Http.IResult;

namespace Exceptionless.Web.Api.Results;

/// <summary>
/// Extension methods to convert Foundatio.Mediator Result types to ASP.NET IResult.
/// Used in endpoint lambdas after invoking handlers via the mediator.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a Result (non-generic) to an HTTP IResult.
    /// </summary>
    public static IHttpResult ToHttpResult(this Result result)
    {
        return result.Status switch
        {
            ResultStatus.Success => HttpResults.Ok(),
            ResultStatus.Created => HttpResults.Created(result.Location, null),
            ResultStatus.Accepted => HttpResults.StatusCode(StatusCodes.Status202Accepted),
            ResultStatus.NoContent => HttpResults.NoContent(),
            ResultStatus.NotFound => HttpResults.Problem(statusCode: StatusCodes.Status404NotFound, title: result.Message ?? "Not Found"),
            ResultStatus.Forbidden => HttpResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: result.Message ?? "Forbidden"),
            ResultStatus.Unauthorized => HttpResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: result.Message ?? "Unauthorized"),
            ResultStatus.BadRequest => HttpResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Message ?? "Bad Request"),
            ResultStatus.Conflict => HttpResults.Problem(statusCode: StatusCodes.Status409Conflict, title: result.Message ?? "Conflict"),
            ResultStatus.Invalid => MapValidation(result),
            ResultStatus.Error => HttpResults.Problem(detail: result.Message, statusCode: StatusCodes.Status500InternalServerError),
            ResultStatus.CriticalError => HttpResults.Problem(detail: result.Message, statusCode: StatusCodes.Status500InternalServerError),
            ResultStatus.Unavailable => HttpResults.Problem(detail: result.Message, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => HttpResults.Problem(detail: result.Message ?? "An unexpected error occurred", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    /// <summary>
    /// Converts a Result&lt;T&gt; to an HTTP IResult with the value as the body.
    /// </summary>
    public static IHttpResult ToHttpResult<T>(this Result<T> result)
    {
        if (!result.IsSuccess)
            return ((Foundatio.Mediator.IResult)result).ToHttpResultError();

        var value = result.ValueOrDefault;
        if (value is null)
        {
            return result.Status switch
            {
                ResultStatus.Accepted => HttpResults.StatusCode(StatusCodes.Status202Accepted),
                ResultStatus.Created => HttpResults.Created(result.Location, null),
                ResultStatus.NoContent => HttpResults.NoContent(),
                _ => HttpResults.Ok()
            };
        }

        if (value is IPagedResult paged)
            return new PagedHttpResult(paged);

        if (value is NotModifiedResponse)
            return HttpResults.StatusCode(StatusCodes.Status304NotModified);

        // ModelActionResults with failures returns 400 BadRequest (preserving legacy behavior)
        if (value is Controllers.ModelActionResults { Failure.Count: > 0 } modelAction)
            return HttpResults.Json(modelAction, statusCode: StatusCodes.Status400BadRequest);

        // WorkInProgressResult (and ModelActionResults with no failures) returns 202 Accepted
        if (value is Controllers.WorkInProgressResult)
            return HttpResults.Json(value, statusCode: StatusCodes.Status202Accepted);

        return result.Status switch
        {
            ResultStatus.Accepted => HttpResults.Json(value, statusCode: StatusCodes.Status202Accepted),
            ResultStatus.Created => HttpResults.Created(result.Location, value),
            _ => HttpResults.Ok(value)
        };
    }

    private static IHttpResult ToHttpResultError(this Foundatio.Mediator.IResult result)
    {
        return result.Status switch
        {
            ResultStatus.NotFound => HttpResults.Problem(statusCode: StatusCodes.Status404NotFound, title: result.Message ?? "Not Found"),
            ResultStatus.Forbidden => HttpResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: result.Message ?? "Forbidden"),
            ResultStatus.Unauthorized => HttpResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: result.Message ?? "Unauthorized"),
            ResultStatus.BadRequest => HttpResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Message ?? "Bad Request"),
            ResultStatus.Conflict => HttpResults.Problem(statusCode: StatusCodes.Status409Conflict, title: result.Message ?? "Conflict"),
            ResultStatus.Invalid => MapValidation(result),
            ResultStatus.Error => HttpResults.Problem(detail: result.Message, statusCode: StatusCodes.Status500InternalServerError),
            ResultStatus.CriticalError => HttpResults.Problem(detail: result.Message, statusCode: StatusCodes.Status500InternalServerError),
            ResultStatus.Unavailable => HttpResults.Problem(detail: result.Message, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => HttpResults.Problem(detail: result.Message ?? "An unexpected error occurred", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static IHttpResult MapValidation(Foundatio.Mediator.IResult result)
    {
        var errors = result.ValidationErrors?.ToList();
        if (errors is null || errors.Count == 0)
            return HttpResults.Problem(detail: result.Message, statusCode: StatusCodes.Status422UnprocessableEntity, title: "Validation failed");

        var planLimitError = errors.FirstOrDefault(error => String.Equals(error.Identifier, "plan_limit", StringComparison.OrdinalIgnoreCase));
        if (planLimitError is not null)
            return HttpResults.Problem(statusCode: StatusCodes.Status426UpgradeRequired, title: planLimitError.ErrorMessage);

        var notImplementedError = errors.FirstOrDefault(error => String.Equals(error.Identifier, "not_implemented", StringComparison.OrdinalIgnoreCase));
        if (notImplementedError is not null)
            return HttpResults.Problem(statusCode: StatusCodes.Status501NotImplemented, title: notImplementedError.ErrorMessage);

        var rateLimitError = errors.FirstOrDefault(error => String.Equals(error.Identifier, "rate_limit", StringComparison.OrdinalIgnoreCase));
        if (rateLimitError is not null)
            return HttpResults.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: rateLimitError.ErrorMessage);

        var errorDict = new Dictionary<string, string[]>();
        foreach (var error in errors)
        {
            var key = error.Identifier ?? "";
            errorDict[key] = errorDict.TryGetValue(key, out var existing)
                ? [.. existing, error.ErrorMessage]
                : [error.ErrorMessage];
        }

        return HttpResults.ValidationProblem(errorDict, title: result.Message ?? "Validation failed", statusCode: StatusCodes.Status422UnprocessableEntity);
    }
}
