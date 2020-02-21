using System.Threading.Tasks;
using Exceptionless.Core.Utility;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests {
    public class SemanticVersionTests : TestWithServices {
        private readonly SemanticVersionParser _parser;
        
        public SemanticVersionTests(ServicesFixture fixture, ITestOutputHelper output) : base(fixture, output) {
            _parser = new SemanticVersionParser(Log);
        }
        
        [Theory]
        [InlineData(null, null)]
        [InlineData("a.b.c.d", null)]
        [InlineData("1.b", null)]
        [InlineData("test", null)]
        [InlineData("1", "1.0.0")]
        [InlineData(" 1 ", "1.0.0")]
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
        [InlineData("4.1.0034", "4.1.34")]
        public async Task CanParseSemanticVersion(string input, string expected) {
            var actual = await _parser.ParseAsync(input);
            Assert.Equal(expected, actual?.ToString());
        }
        
        [Theory]
        [InlineData("4.1.0034", "4.1.34")]
        public async Task VerifySameSemanticVersion(string version1, string version2) {
            var parsedVersion1 = await _parser.ParseAsync(version1);
            var parsedVersion2 = await _parser.ParseAsync(version2);
            Assert.Equal(parsedVersion1, parsedVersion2);
        }
        
        [Theory]
        [InlineData("4.1.0034", "4.1.35")]
        public async Task VerifySemanticVersionIsNewer(string oldVersion, string newVersion) {
            var parsedOldVersion = await _parser.ParseAsync(oldVersion);
            var parsedNewVersion = await _parser.ParseAsync(newVersion);
            Assert.True(parsedOldVersion < parsedNewVersion);
        }
    }
}
