using Exceptionless.Core.Extensions;
using Exceptionless.Web.Controllers;
using Foundatio.Mediator;
using Microsoft.AspNetCore.Http;
using HttpResults = Microsoft.AspNetCore.Http.Results;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Exceptionless.Web.Api.Results;

public static class ApiValidationErrorIdentifiers
{
    public const string PlanLimit = "plan_limit";
    public const string NotImplemented = "not_implemented";
    public const string RateLimit = "rate_limit";
    public const string RequestEntityTooLarge = "request_entity_too_large";
}

/// <summary>
/// Maps Foundatio.Mediator Result types to ASP.NET Core IResult HTTP responses.
/// Registered before AddMediator() to customize how Result statuses become HTTP responses.
/// Preserves existing ProblemDetails shape (instance, reference-id, errors with snake_case keys).
/// </summary>
public sealed class ApiResultMapper : IMediatorResultMapper<IResult>
{
    private readonly MediatorResultMapperOptions<IResult>? _options;

    public ApiResultMapper(MediatorResultMapperOptions<IResult>? options = null)
    {
        _options = options;
    }

    public IResult MapResult(Foundatio.Mediator.IResult result)
    {
        if (result.Status is ResultStatus.Success)
            return MapSuccess(result);

        if (result.Status is ResultStatus.Created)
            return MapCreated(result);

        if (result.Status is ResultStatus.Accepted)
            return MapAccepted(result);

        if (_options?.TryMap(result, out var mappedResult) == true)
            return mappedResult;

        if (result.Status is ResultStatus.NoContent)
            return HttpResults.NoContent();

        return HttpResults.Problem(
            detail: result.Message ?? "An unexpected error occurred", statusCode: StatusCodes.Status500InternalServerError);
    }

    public static IResult MapBadRequest(Foundatio.Mediator.IResult result)
    {
        return HttpResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Message ?? "Bad Request");
    }

    public static IResult MapNotFound(Foundatio.Mediator.IResult result)
    {
        return HttpResults.Problem(statusCode: StatusCodes.Status404NotFound, title: result.Message ?? "Not Found");
    }

    public static IResult MapUnauthorized(Foundatio.Mediator.IResult result)
    {
        return HttpResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: result.Message ?? "Unauthorized");
    }

    public static IResult MapForbidden(Foundatio.Mediator.IResult result)
    {
        return HttpResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: result.Message ?? "Forbidden");
    }

    public static IResult MapConflict(Foundatio.Mediator.IResult result)
    {
        return HttpResults.Problem(statusCode: StatusCodes.Status409Conflict, title: result.Message ?? "Conflict");
    }

    public static IResult MapError(Foundatio.Mediator.IResult result)
    {
        return HttpResults.Problem(
            detail: result.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Internal Server Error");
    }

    public static IResult MapCriticalError(Foundatio.Mediator.IResult result)
    {
        return HttpResults.Problem(
            detail: result.Message, statusCode: StatusCodes.Status500InternalServerError, title: "Critical Error");
    }

    public static IResult MapUnavailable(Foundatio.Mediator.IResult result)
    {
        return HttpResults.Problem(
            detail: result.Message, statusCode: StatusCodes.Status503ServiceUnavailable, title: "Service Unavailable");
    }

    public static IResult MapValidation(Foundatio.Mediator.IResult result)
    {
        string title = String.IsNullOrWhiteSpace(result.Message)
            ? "One or more validation errors occurred."
            : result.Message;
        var errors = result.ValidationErrors?.ToList();
        if (errors is null || errors.Count == 0)
            return HttpResults.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity, title: title);

        var planLimitError = errors.FirstOrDefault(error => String.Equals(error.Identifier, ApiValidationErrorIdentifiers.PlanLimit, StringComparison.OrdinalIgnoreCase));
        if (planLimitError is not null)
            return HttpResults.Problem(statusCode: StatusCodes.Status426UpgradeRequired, title: planLimitError.ErrorMessage);

        var notImplementedError = errors.FirstOrDefault(error => String.Equals(error.Identifier, ApiValidationErrorIdentifiers.NotImplemented, StringComparison.OrdinalIgnoreCase));
        if (notImplementedError is not null)
            return HttpResults.Problem(statusCode: StatusCodes.Status501NotImplemented, title: notImplementedError.ErrorMessage);

        var rateLimitError = errors.FirstOrDefault(error => String.Equals(error.Identifier, ApiValidationErrorIdentifiers.RateLimit, StringComparison.OrdinalIgnoreCase));
        if (rateLimitError is not null)
            return HttpResults.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: rateLimitError.ErrorMessage);

        var requestEntityTooLargeError = errors.FirstOrDefault(error => String.Equals(error.Identifier, ApiValidationErrorIdentifiers.RequestEntityTooLarge, StringComparison.OrdinalIgnoreCase));
        if (requestEntityTooLargeError is not null)
            return HttpResults.Problem(statusCode: StatusCodes.Status413RequestEntityTooLarge, title: requestEntityTooLargeError.ErrorMessage);

        var errorDict = errors
            .GroupBy(error => (error.Identifier ?? String.Empty).ToLowerUnderscoredWords(), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.Ordinal);

        return HttpResults.ValidationProblem(errorDict, title: title, statusCode: StatusCodes.Status422UnprocessableEntity);
    }

    private static IResult MapSuccess(Foundatio.Mediator.IResult result)
    {
        var value = result.GetValue();
        if (value is null)
            return HttpResults.Ok();

        // Handle PagedResult<T> — serialize Items and set pagination headers
        if (value is IPagedResult paged)
            return new PagedHttpResult(paged);

        if (value is NotModifiedResponse)
            return HttpResults.StatusCode(StatusCodes.Status304NotModified);

        if (value is ModelActionResults modelAction)
        {
            if (modelAction.Failure.Count > 0)
                return HttpResults.Json(modelAction, statusCode: StatusCodes.Status400BadRequest);

            return HttpResults.Json(new WorkInProgressResult(modelAction.Workers), statusCode: StatusCodes.Status202Accepted);
        }

        if (value is WorkInProgressResult)
            return HttpResults.Json(value, statusCode: StatusCodes.Status202Accepted);

        return HttpResults.Ok(value);
    }

    private static IResult MapCreated(Foundatio.Mediator.IResult result)
    {
        var value = result.GetValue();
        var location = result.Location;
        return HttpResults.Created(location, value);
    }

    private static IResult MapAccepted(Foundatio.Mediator.IResult result)
    {
        return HttpResults.StatusCode(StatusCodes.Status202Accepted);
    }
}
