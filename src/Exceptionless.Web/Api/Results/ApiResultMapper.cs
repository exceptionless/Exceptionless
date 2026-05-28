using Foundatio.Mediator;
using Microsoft.AspNetCore.Http;
using HttpResults = Microsoft.AspNetCore.Http.Results;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Exceptionless.Web.Api.Results;

/// <summary>
/// Maps Foundatio.Mediator Result types to ASP.NET Core IResult HTTP responses.
/// Registered before AddMediator() to customize how Result statuses become HTTP responses.
/// Preserves existing ProblemDetails shape (instance, reference-id, errors with snake_case keys).
/// </summary>
public sealed class ApiResultMapper : IMediatorResultMapper<IResult>
{
    public IResult MapResult(Foundatio.Mediator.IResult result)
    {
        return result.Status switch
        {
            ResultStatus.Success => MapSuccess(result),
            ResultStatus.Created => MapCreated(result),
            ResultStatus.NoContent => HttpResults.NoContent(),
            ResultStatus.BadRequest => HttpResults.Problem(
                detail: result.Message, statusCode: StatusCodes.Status400BadRequest, title: "Bad Request"),
            ResultStatus.Invalid => MapValidation(result),
            ResultStatus.NotFound => HttpResults.Problem(
                detail: result.Message, statusCode: StatusCodes.Status404NotFound, title: "Not Found"),
            ResultStatus.Unauthorized => HttpResults.Problem(
                detail: result.Message, statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized"),
            ResultStatus.Forbidden => HttpResults.Problem(
                detail: result.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden"),
            ResultStatus.Conflict => HttpResults.Problem(
                detail: result.Message, statusCode: StatusCodes.Status409Conflict, title: "Conflict"),
            ResultStatus.Error => HttpResults.Problem(
                detail: result.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Internal Server Error"),
            ResultStatus.CriticalError => HttpResults.Problem(
                detail: result.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Critical Error"),
            ResultStatus.Unavailable => HttpResults.Problem(
                detail: result.Message, statusCode: StatusCodes.Status503ServiceUnavailable, title: "Service Unavailable"),
            _ => HttpResults.Problem(
                detail: result.Message ?? "An unexpected error occurred", statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    private static IResult MapSuccess(Foundatio.Mediator.IResult result)
    {
        var value = GetValue(result);
        if (value is null)
            return HttpResults.Ok();

        // Handle PagedResult<T> — serialize Items and set pagination headers
        if (value is IPagedResult paged)
            return new PagedHttpResult(paged);

        if (value is NotModifiedResponse)
            return HttpResults.StatusCode(StatusCodes.Status304NotModified);

        // Handle WorkInProgressResponse
        if (value is WorkInProgressResponse wip)
            return HttpResults.Json(new { workers = wip.Workers }, statusCode: StatusCodes.Status202Accepted);

        return HttpResults.Ok(value);
    }

    private static IResult MapCreated(Foundatio.Mediator.IResult result)
    {
        var value = GetValue(result);
        var location = result.Location;
        return HttpResults.Created(location, value);
    }

    private static IResult MapValidation(Foundatio.Mediator.IResult result)
    {
        var errors = result.ValidationErrors?.ToList();
        if (errors is null || errors.Count == 0)
            return HttpResults.Problem(
                detail: result.Message, statusCode: StatusCodes.Status400BadRequest, title: "Validation failed");

        var planLimitError = errors.FirstOrDefault(error => String.Equals(error.Identifier, "plan_limit", StringComparison.OrdinalIgnoreCase));
        if (planLimitError is not null)
            return HttpResults.Problem(statusCode: StatusCodes.Status426UpgradeRequired, title: planLimitError.ErrorMessage);

        var rateLimitError = errors.FirstOrDefault(error => String.Equals(error.Identifier, "rate_limit", StringComparison.OrdinalIgnoreCase));
        if (rateLimitError is not null)
            return HttpResults.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: rateLimitError.ErrorMessage);

        // Convert to dictionary format matching existing ProblemDetails shape
        var errorDict = new Dictionary<string, string[]>();
        foreach (var error in errors)
        {
            var key = error.Identifier ?? "";
            if (errorDict.TryGetValue(key, out var existing))
                errorDict[key] = [.. existing, error.ErrorMessage];
            else
                errorDict[key] = [error.ErrorMessage];
        }

        return HttpResults.ValidationProblem(errorDict, title: result.Message ?? "Validation failed");
    }

    private static object? GetValue(Foundatio.Mediator.IResult result)
    {
        // Use reflection to get Value from Result<T>
        var type = result.GetType();
        var valueProp = type.GetProperty("ValueOrDefault");
        return valueProp?.GetValue(result);
    }
}
