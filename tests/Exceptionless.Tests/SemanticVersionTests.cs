using Exceptionless.Core.Utility;
using McSherry.SemanticVersioning;
using Xunit;

namespace Exceptionless.Tests;

public class SemanticVersionTests : TestWithServices
{
    private readonly SemanticVersionParser _parser;

    public SemanticVersionTests(ITestOutputHelper output) : base(output)
    {
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
    [InlineData("1.2.3*.0", "1.2.3")]
    [InlineData("1.2.*.0", "1.2.0")]
    [InlineData("1.2.*", "1.2.0")]
    [InlineData("1.2.3.4", "1.2.3-4")]
    [InlineData("1.2.3.4 7ab3b4da18", "1.2.3-4")]
    [InlineData("4.1.0034", "4.1.34")]
    [InlineData("21.0.717+build20220623120110+commit32432423423", "21.0.717")]
    public void CanParseSemanticVersion(string? input, string? expected)
    {
        var actual = _parser.Parse(input);
        Assert.Equal(expected, actual?.ToString());
    }

    [Theory]
    [InlineData("4.1.0034", "4.1.34")]
    public void VerifySameSemanticVersion(string version1, string version2)
    {
        var parsedVersion1 = _parser.Parse(version1);
        var parsedVersion2 = _parser.Parse(version2);
        Assert.Equal(parsedVersion1, parsedVersion2);
    }

    [Theory]
    [InlineData("4.1.0034", "4.1.35")]
    public void VerifySemanticVersionIsNewer(string oldVersion, string newVersion)
    {
        var parsedOldVersion = _parser.Parse(oldVersion);
        var parsedNewVersion = _parser.Parse(newVersion);
        Assert.True(parsedOldVersion < parsedNewVersion);
    }

    [Fact]
    public void CanUseVersionCache()
    {
        var nonCached1 = _parser.Parse("4.1.0034");
        var nonCached2 = _parser.Parse("4.1.0034");

        Assert.NotSame(nonCached1, nonCached2);

        var versionCache = new Dictionary<string, SemanticVersion>();
        var cached1 = _parser.Parse("4.1.0034", versionCache);
        var cached2 = _parser.Parse("4.1.0034", versionCache);

        Assert.Same(cached1, cached2);
    }
}
