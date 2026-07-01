using System.Collections.Specialized;
using System.Web;
using Exceptionless.Core.Extensions;
using Exceptionless.Web.Utility;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Web.Api.Results;

public static class ApiResults
{
    public static IResult OkWithLinks<T>(T content, params string?[] links)
    {
        var validLinks = links.Where(l => !String.IsNullOrEmpty(l)).ToArray();
        return new OkWithLinksResult<T>(content, validLinks!);
    }

    public static IResult OkWithResourceLinks<TEntity>(HttpContext context, ICollection<TEntity> content, bool hasMore, int? page = null, long? total = null, string? before = null, string? after = null) where TEntity : class
    {
        var headers = new Dictionary<string, string[]>();

        if (total.HasValue)
            headers[Headers.ResultCount] = [total.Value.ToString()];

        var linkValues = page.HasValue
            ? GetPagedLinks(new Uri(context.Request.GetDisplayUrl()), page.Value, hasMore)
            : GetBeforeAndAfterLinks(new Uri(context.Request.GetDisplayUrl()), before, after, hasMore);

        if (linkValues.Count > 0)
            headers[HeaderNames.Link.ToString()] = linkValues.ToArray();

        return new OkWithHeadersResult<ICollection<TEntity>>(content, headers);
    }

    public static IResult WorkInProgress(IEnumerable<string> workers)
    {
        return TypedResults.Json(new { workers = workers.ToArray() }, statusCode: StatusCodes.Status202Accepted);
    }

    public static IResult Permission(PermissionResult permission)
    {
        if (String.IsNullOrEmpty(permission.Message))
            return TypedResults.Problem(statusCode: permission.StatusCode);

        return TypedResults.Problem(statusCode: permission.StatusCode, title: permission.Message);
    }

    public static IResult PlanLimitReached(string message)
    {
        return TypedResults.Problem(statusCode: StatusCodes.Status426UpgradeRequired, title: message);
    }

    public static IResult Forbidden(string? message = null)
    {
        if (String.IsNullOrEmpty(message))
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: message);
    }

    public static IResult TooManyRequests(string message)
    {
        return TypedResults.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: message);
    }

    public static IResult NotImplemented(string message)
    {
        return TypedResults.Problem(statusCode: StatusCodes.Status501NotImplemented, title: message);
    }

    public static List<string> GetPagedLinks(Uri url, int page, bool hasMore)
    {
        bool includePrevious = page > 1;
        bool includeNext = hasMore;

        var previousParameters = HttpUtility.ParseQueryString(url.Query);
        previousParameters["page"] = (page - 1).ToString();
        var nextParameters = new NameValueCollection(previousParameters)
        {
            ["page"] = (page + 1).ToString()
        };

        string baseUrl = url.GetBaseUrl();

        var links = new List<string>(2);
        if (includePrevious)
            links.Add($"<{baseUrl}?{previousParameters.ToQueryString()}>; rel=\"previous\"");
        if (includeNext)
            links.Add($"<{baseUrl}?{nextParameters.ToQueryString()}>; rel=\"next\"");

        return links;
    }

    public static List<string> GetBeforeAndAfterLinks(Uri url, string? before, string? after, bool hasMore)
    {
        var previousParameters = HttpUtility.ParseQueryString(url.Query);
        previousParameters.Remove("before");
        previousParameters.Remove("after");

        var nextParameters = new NameValueCollection(previousParameters);
        previousParameters.Add("before", before);
        nextParameters.Add("after", after);

        string baseUrl = url.GetBaseUrl();
        var links = new List<string>(2);
        if (!String.IsNullOrEmpty(before))
            links.Add($"<{baseUrl}?{previousParameters.ToQueryString()}>; rel=\"previous\"");
        if (hasMore && !String.IsNullOrEmpty(after))
            links.Add($"<{baseUrl}?{nextParameters.ToQueryString()}>; rel=\"next\"");

        return links;
    }
}

public class OkWithLinksResult<T> : IResult
{
    private readonly T _content;
    private readonly string[] _links;

    public OkWithLinksResult(T content, string[] links)
    {
        _content = content;
        _links = links;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        if (_links.Length > 0)
            httpContext.Response.Headers[HeaderNames.Link] = _links;

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        return httpContext.Response.WriteAsJsonAsync(_content);
    }
}

public class OkWithHeadersResult<T> : IResult
{
    private readonly T _content;
    private readonly Dictionary<string, string[]> _headers;

    public OkWithHeadersResult(T content, Dictionary<string, string[]> headers)
    {
        _content = content;
        _headers = headers;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        foreach (var header in _headers)
            httpContext.Response.Headers[header.Key] = header.Value;

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        return httpContext.Response.WriteAsJsonAsync(_content);
    }
}

public record PermissionResult
{
    public bool Allowed { get; init; }
    public string? Id { get; init; }
    public string? Message { get; init; }
    public int StatusCode { get; init; } = StatusCodes.Status200OK;

    public static PermissionResult Allow => new() { Allowed = true };
    public static PermissionResult Deny => new() { Allowed = false, StatusCode = StatusCodes.Status403Forbidden };

    public static PermissionResult DenyWithMessage(string message, int statusCode = StatusCodes.Status403Forbidden)
        => new() { Allowed = false, Message = message, StatusCode = statusCode };

    public static PermissionResult DenyWithStatus(int statusCode)
        => new() { Allowed = false, StatusCode = statusCode };

    public static PermissionResult DenyWithNotFound(string? id = null)
        => new() { Allowed = false, Id = id, StatusCode = StatusCodes.Status404NotFound };

    public static PermissionResult DenyWithPlanLimitReached(string message)
        => new() { Allowed = false, Message = message, StatusCode = StatusCodes.Status426UpgradeRequired };
}
