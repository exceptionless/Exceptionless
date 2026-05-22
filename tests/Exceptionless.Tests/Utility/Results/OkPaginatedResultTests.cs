using Exceptionless.Web.Utility.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Exceptionless.Tests.Utility.Results;

public sealed class OkPaginatedResultTests
{
    [Fact]
    public void AddPageLinkHeaders_AddsPreviousAndNextLinks()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v2/events";
        context.Request.QueryString = new QueryString("?filter=type:error&page=2&limit=10");
        var result = new OkPaginatedResult(Array.Empty<object>(), hasMore: true, page: 2);

        // Act
        result.AddPageLinkHeaders(context.Request);

        // Assert
        string?[] links = result.Headers[HeaderNames.Link].ToArray();
        Assert.Equal(2, links.Length);
        Assert.Contains(links, l => l is not null && l.Contains("page=1", StringComparison.Ordinal) && l.Contains("rel=\"previous\"", StringComparison.Ordinal));
        Assert.Contains(links, l => l is not null && l.Contains("page=3", StringComparison.Ordinal) && l.Contains("rel=\"next\"", StringComparison.Ordinal));
    }

    [Fact]
    public void OnFormatting_AddsPaginationAndTotalHeaders()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v2/events";
        httpContext.Request.QueryString = new QueryString("?page=1");

        var result = new OkPaginatedResult(Array.Empty<object>(), hasMore: true, page: 1, total: 42);
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        // Act
        result.OnFormatting(actionContext);

        // Assert
        Assert.Equal("42", httpContext.Response.Headers[Exceptionless.Web.Utility.Headers.ResultCount]);
        Assert.Contains("page=2", httpContext.Response.Headers[HeaderNames.Link].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AddPageLinkHeaders_NoAdjacentPages_DoesNotAddHeader()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v2/events";
        var result = new OkPaginatedResult(Array.Empty<object>(), hasMore: false, page: 1);

        // Act
        result.AddPageLinkHeaders(context.Request);

        // Assert
        Assert.False(result.Headers.ContainsKey(HeaderNames.Link));
    }
}
