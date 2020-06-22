using System;
using System.Collections.Generic;
using Exceptionless.Web.Utility.Results;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Tests.Miscellaneous {
    public class EfficientPagingTests : TestWithServices {
        public EfficientPagingTests(ITestOutputHelper output) : base(output) {}

        [Theory]
        [InlineData("http://localhost", false, false, false)]
        [InlineData("http://localhost", true, false, true)]
        [InlineData("http://localhost?after=1", false, true, false)]
        [InlineData("http://localhost?after=1", true, true, true)]
        [InlineData("http://localhost?before=11", false, false, true)]
        [InlineData("http://localhost?before=11", true, true, true)]
        public void CanBeforeAndAfterLinks(string url, bool hasMore, bool expectPrevious, bool expectNext){
            var data = new List<string> { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11" };

            var links = OkWithResourceLinks<string>.GetBeforeAndAfterLinks(new Uri(url), data, false, hasMore, s => s);
            int expectedLinkCount = 0;
            if (expectPrevious)
                expectedLinkCount++;
            if (expectNext)
                expectedLinkCount++;

            foreach (string link in links)
                _logger.LogInformation(link);

            Assert.Equal(expectedLinkCount, links.Count);
            if (expectPrevious)
                Assert.Contains(links, l => l.Contains("previous"));
            if (expectNext)
                Assert.Contains(links, l => l.Contains("next"));
        }

        [Theory]
        [InlineData("http://localhost", 0, false, false, false)]
        [InlineData("http://localhost", 1, false, false, false)]
        [InlineData("http://localhost", 2, false, true, false)]
        [InlineData("http://localhost", 2, true, true, true)]
        public void CanPageLinks(string url, int pageNumber, bool hasMore, bool expectPrevious, bool expectNext){
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
                Assert.Contains(links, l => l.Contains("previous"));
            if (expectNext)
                Assert.Contains(links, l => l.Contains("next"));
        }
    }
}