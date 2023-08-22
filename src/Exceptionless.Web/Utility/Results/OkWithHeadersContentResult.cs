using System.Collections.Specialized;
using System.Web;
using Exceptionless.Core.Extensions;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Exceptionless.Web.Utility.Results;

public class OkWithHeadersContentResult<T> : ObjectWithHeadersResult
{
    public OkWithHeadersContentResult(T? content, IHeaderDictionary? headers = null) : base(content, headers)
    {
        StatusCode = StatusCodes.Status200OK;
    }
}

public class OkWithResourceLinks<TEntity> : OkWithHeadersContentResult<ICollection<TEntity>> where TEntity : class
{
    public OkWithResourceLinks(ICollection<TEntity> content, bool hasMore, int? page = null, string? before = null, string? after = null)
        : this(content, hasMore, page, null, before, after) { }

    public OkWithResourceLinks(ICollection<TEntity> content, bool hasMore, int? page = null, long? total = null, string? before = null, string? after = null) : base(content)
    {
        Content = content;
        HasMore = hasMore;
        Before = before;
        After = after;
        Page = page;
        Total = total;
    }

    public ICollection<TEntity> Content { get; }
    public bool HasMore { get; }
    public string? Before { get; }
    public string? After { get; }
    public int? Page { get; }
    public long? Total { get; }

    public override void OnFormatting(ActionContext context)
    {
        var links = Page.HasValue
            ? GetPagedLinks(new Uri(context.HttpContext.Request.GetDisplayUrl()), Page.Value, HasMore)
            : GetBeforeAndAfterLinks(new Uri(context.HttpContext.Request.GetDisplayUrl()), Before, After);
        if (links.Count > 0)
            Headers.Add(Utility.Headers.Link, links.ToArray());

        if (Total.HasValue)
            Headers.Add(Utility.Headers.ResultCount, Total.ToString());

        base.OnFormatting(context);
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

    public static List<string> GetBeforeAndAfterLinks(Uri url, string? before, string? after)
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
        if (!String.IsNullOrEmpty(after))
            links.Add($"<{baseUrl}?{nextParameters.ToQueryString()}>; rel=\"next\"");

        return links;
    }
}
