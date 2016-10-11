using System;
using System.Threading.Tasks;
using Exceptionless.Core.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests {
    public class SemanticVersionParserTests : TestBase {
        private readonly SemanticVersionParser _parser;
        public SemanticVersionParserTests(ITestOutputHelper output) : base(output) {
            _parser = new SemanticVersionParser(Log);
        }
        
        [Theory]
        [InlineData(null, null)]
        [InlineData("1", "1.0.0")]
        [InlineData("1.2", "1.2.0")]
        [InlineData("1.2 7ab3b4da18", "1.2.0")]
        [InlineData("1.2.3", "1.2.3")]
        [InlineData("1.2.3 7ab3b4da18", "1.2.3")]
        [InlineData("1.2.3-beta2", "1.2.3-beta2")]
        [InlineData("1.2.3.*", "1.2.3")]
        [InlineData("1.2.3.0", "1.2.3-0")]
        [InlineData("1.2.3.0*", "1.2.3-0")]
        [InlineData("1.2.3*.0", "1.2.3-0")]
        [InlineData("1.2.*.0", "1.2.0")]
        [InlineData("1.2.*", "1.2.0")]
        [InlineData("1.2.3.4", "1.2.3-4")]
        [InlineData("1.2.3.4 7ab3b4da18", "1.2.3-4")]
        public async Task SemanticVersionTests(string input, string expected) {
            var actual = await _parser.ParseAsync(input);
            Assert.Equal(expected, actual?.ToString());
        }
    }
}
