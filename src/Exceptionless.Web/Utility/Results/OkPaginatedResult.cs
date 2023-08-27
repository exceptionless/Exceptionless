using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Exceptionless.Web.Utility.Results;

public class OkPaginatedResult : ObjectWithHeadersResult
{
    public OkPaginatedResult(object? content, bool hasMore, int page, long? total = null, IHeaderDictionary? headers = null) : base(content, headers)
    {
        StatusCode = StatusCodes.Status200OK;
        HasMore = hasMore;
        Page = page;
        Total = total;
    }

    public bool HasMore { get; set; }
    public int Page { get; set; }
    public long? Total { get; set; }

    public override void OnFormatting(ActionContext context)
    {
        AddPageLinkHeaders(context.HttpContext.Request);

        if (Total.HasValue)
            Headers.Add(Utility.Headers.ResultCount, Total.ToString());

        base.OnFormatting(context);
    }

    public void AddPageLinkHeaders(HttpRequest request)
    {
        bool includePrevious = Page > 1;
        bool includeNext = HasMore;

        if (!includePrevious && !includeNext)
            return;

        var links = new List<string>(2);
        if (includePrevious)
        {
            var previousParameters = new Dictionary<string, StringValues>(request.Query)
            {
                ["page"] = (Page - 1).ToString()
            };
            links.Add(String.Concat("<", request.Path, "?", String.Join('&', previousParameters.Select(kvp => $"{kvp.Key}={kvp.Value}")), ">; rel=\"previous\""));
        }

        if (includeNext)
        {
            var nextParameters = new Dictionary<string, StringValues>(request.Query)
            {
                ["page"] = (Page + 1).ToString()
            };

            links.Add(String.Concat("<", request.Path, "?", String.Join('&', nextParameters.Select(kvp => $"{kvp.Key}={kvp.Value}")), ">; rel=\"next\""));
        }

        Headers.Add(HeaderNames.Link, links.ToArray());
    }
}
