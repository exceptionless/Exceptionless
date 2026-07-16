using Exceptionless.Web.Utility;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Web.Api.Results;

/// <summary>
/// Interface for paginated result detection in the result mapper.
/// </summary>
public interface IPagedResult
{
    object Items { get; }
    bool HasMore { get; }
    int? Page { get; }
    long? Total { get; }
    string? Before { get; }
    string? After { get; }
}

/// <summary>
/// Transport-agnostic paginated response. Handlers return this; the mapper
/// serializes only Items and projects metadata into HTTP headers.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    bool HasMore,
    int? Page = null,
    long? Total = null,
    string? Before = null,
    string? After = null) : IPagedResult where T : class
{
    object IPagedResult.Items => Items;
}

/// <summary>
/// Custom IResult that writes pagination headers (Link, X-Result-Count) and serializes items.
/// </summary>
internal sealed class PagedHttpResult : IResult
{
    private readonly IPagedResult _paged;

    public PagedHttpResult(IPagedResult paged) => _paged = paged;

    public Task ExecuteAsync(HttpContext httpContext)
    {
        if (_paged.Total.HasValue)
            httpContext.Response.Headers[Headers.ResultCount] = _paged.Total.Value.ToString();

        var linkValues = _paged.Page.HasValue
            ? ApiResults.GetPagedLinks(new Uri(httpContext.Request.GetDisplayUrl()), _paged.Page.Value, _paged.HasMore)
            : ApiResults.GetBeforeAndAfterLinks(new Uri(httpContext.Request.GetDisplayUrl()), _paged.Before, _paged.After, _paged.HasMore);

        if (linkValues.Count > 0)
            httpContext.Response.Headers[HeaderNames.Link.ToString()] = linkValues.ToArray();

        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        return httpContext.Response.WriteAsJsonAsync(_paged.Items);
    }
}
