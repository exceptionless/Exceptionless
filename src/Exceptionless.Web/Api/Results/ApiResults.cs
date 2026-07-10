using System.Collections.Specialized;
using System.Web;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Web.Api.Results;

public static class ApiResults
{
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

    public static IResult TooManyRequests(string message)
    {
        return TypedResults.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: message);
    }
}
