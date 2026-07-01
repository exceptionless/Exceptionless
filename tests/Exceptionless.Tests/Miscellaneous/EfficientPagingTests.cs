using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Utility.Results;
using Xunit;

namespace Exceptionless.Tests.Miscellaneous;

public class EfficientPagingTests : TestWithServices
{
    public EfficientPagingTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData("http://localhost", null, null, false, false)]
    [InlineData("http://localhost?after=1", "1", null, true, false)]
    [InlineData("http://localhost?after=1", "1", "2", true, true)]
    [InlineData("http://localhost?before=11", null, "1", false, true)]
    public void GetBeforeAndAfterLinks_WithCursorTokens_ReturnsExpectedDirectionalLinks(string url, string? before, string? after, bool expectPrevious, bool expectNext)
    {
        // Arrange
        byte expectedLinkCount = 0;
        if (expectPrevious)
            expectedLinkCount++;
        if (expectNext)
            expectedLinkCount++;

        // Act
        var links = OkWithResourceLinks<string>.GetBeforeAndAfterLinks(new Uri(url), before, after, true);

        // Assert
        foreach (string link in links)
            _logger.LogInformation(link);

        Assert.Equal(expectedLinkCount, links.Count);
        if (expectPrevious)
            Assert.Contains(links, l => l.Contains("previous") && l.Contains("before"));
        if (expectNext)
            Assert.Contains(links, l => l.Contains("next") && l.Contains("after"));
    }

    [Fact]
    public void GetBeforeAndAfterLinks_WithoutMoreResults_DoesNotIncludeNextLink()
    {
        // Arrange
        var url = new Uri("http://localhost?after=1");

        // Act
        var links = OkWithResourceLinks<string>.GetBeforeAndAfterLinks(url, "1", "2", false);

        // Assert
        Assert.Single(links);
        Assert.Contains(links, l => l.Contains("previous") && l.Contains("before"));
        Assert.DoesNotContain(links, l => l.Contains("next"));
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
        var links = OkWithResourceLinks<string>.GetPagedLinks(new Uri(url), pageNumber, hasMore);

        // Assert
        foreach (string link in links)
            _logger.LogInformation(link);

        Assert.Equal(expectedLinkCount, links.Count);
        if (expectPrevious)
            Assert.Contains(links, l => l.Contains("previous") && l.Contains("page"));
        if (expectNext)
            Assert.Contains(links, l => l.Contains("next") && l.Contains("page"));
    }

    [Fact]
    public void ApiResults_GetBeforeAndAfterLinks_WithoutMoreResults_DoesNotIncludeNextLink()
    {
        // Arrange
        var url = new Uri("http://localhost?after=1");

        // Act
        var links = ApiResults.GetBeforeAndAfterLinks(url, "1", "2", false);

        // Assert
        Assert.Single(links);
        Assert.Contains(links, l => l.Contains("previous") && l.Contains("before"));
        Assert.DoesNotContain(links, l => l.Contains("next"));
    }
}
