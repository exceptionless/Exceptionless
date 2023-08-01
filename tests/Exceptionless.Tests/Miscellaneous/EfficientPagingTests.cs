using Exceptionless.Web.Utility.Results;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Miscellaneous;

public class EfficientPagingTests : TestWithServices
{
    public EfficientPagingTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData("http://localhost", null, null, false, false)]
    [InlineData("http://localhost?after=1", "1", null, true, false)]
    [InlineData("http://localhost?after=1", "1", "2", true, true)]
    [InlineData("http://localhost?before=11", null, "1", false, true)]
    public void CanBeforeAndAfterLinks(string url, string before, string after, bool expectPrevious, bool expectNext)
    {
        var links = OkWithResourceLinks<string>.GetBeforeAndAfterLinks(new Uri(url), before, after); ;
        byte expectedLinkCount = 0;
        if (expectPrevious)
            expectedLinkCount++;
        if (expectNext)
            expectedLinkCount++;

        foreach (string link in links)
            _logger.LogInformation(link);

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
    public void CanPageLinks(string url, int pageNumber, bool hasMore, bool expectPrevious, bool expectNext)
    {
        var links = OkWithResourceLinks<string>.GetPagedLinks(new Uri(url), pageNumber, hasMore);

        int expectedLinkCount = 0;
        if (expectPrevious)
            expectedLinkCount++;
        if (expectNext)
            expectedLinkCount++;

        foreach (string link in links)
            _logger.LogInformation(link);

        Assert.Equal(expectedLinkCount, links.Count);
        if (expectPrevious)
            Assert.Contains(links, l => l.Contains("previous") && l.Contains("page"));
        if (expectNext)
            Assert.Contains(links, l => l.Contains("next") && l.Contains("page"));
    }
}
