using Exceptionless.Web.Api.Results;
using Xunit;

namespace Exceptionless.Tests.Api.Results;

public sealed class ApiResultsTests : TestWithServices
{
    public ApiResultsTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void GetBeforeAndAfterLinks_WithoutMoreResults_OmitsNextLink()
    {
        // Arrange
        var url = new Uri("http://localhost?after=1");

        // Act
        var links = ApiResults.GetBeforeAndAfterLinks(url, "1", "2", false);

        // Assert
        foreach (string link in links)
            _logger.LogInformation("API result link: {Link}", link);

        Assert.Single(links);
        Assert.Contains(links, link => link.Contains("previous") && link.Contains("before"));
        Assert.DoesNotContain(links, link => link.Contains("next"));
    }

    [Theory]
    [InlineData("http://localhost", null, null, true, false, false)]
    [InlineData("http://localhost?after=1", "1", null, true, true, false)]
    [InlineData("http://localhost?after=1", "1", "2", true, true, true)]
    [InlineData("http://localhost?before=11", null, "1", true, false, true)]
    public void GetBeforeAndAfterLinks_WithCursorTokens_ReturnsExpectedDirectionalLinks(string url, string? before, string? after, bool hasMore, bool expectPrevious, bool expectNext)
    {
        // Arrange
        byte expectedLinkCount = 0;
        if (expectPrevious)
            expectedLinkCount++;
        if (expectNext)
            expectedLinkCount++;

        // Act
        var links = ApiResults.GetBeforeAndAfterLinks(new Uri(url), before, after, hasMore);

        // Assert
        foreach (string link in links)
            _logger.LogInformation("API result link: {Link}", link);

        Assert.Equal(expectedLinkCount, links.Count);
        if (expectPrevious)
            Assert.Contains(links, l => l.Contains("previous") && l.Contains("before"));
        if (expectNext)
            Assert.Contains(links, l => l.Contains("next") && l.Contains("after"));
    }

    [Theory]
    [InlineData("http://localhost", 0, false, false, false)]
    [InlineData("http://localhost", 1, false, false, false)]
    [InlineData("http://localhost", 2, false, true, false)]
    [InlineData("http://localhost", 2, true, true, true)]
    public void GetPagedLinks_WithPageNumber_ReturnsExpectedDirectionalLinks(string url, int pageNumber, bool hasMore, bool expectPrevious, bool expectNext)
    {
        // Arrange
        int expectedLinkCount = 0;
        if (expectPrevious)
            expectedLinkCount++;
        if (expectNext)
            expectedLinkCount++;

        // Act
        var links = ApiResults.GetPagedLinks(new Uri(url), pageNumber, hasMore);

        // Assert
        foreach (string link in links)
            _logger.LogInformation("API result link: {Link}", link);

        Assert.Equal(expectedLinkCount, links.Count);
        if (expectPrevious)
            Assert.Contains(links, l => l.Contains("previous") && l.Contains("page"));
        if (expectNext)
            Assert.Contains(links, l => l.Contains("next") && l.Contains("page"));
    }
}
