using Exceptionless.Core.Utility;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Exceptionless.Tests.Utility;

public class SemanticVersionParserTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly SemanticVersionParser _parser;

    public SemanticVersionParserTests()
    {
        _loggerFactory = new LoggerFactory();
        _parser = new SemanticVersionParser(_loggerFactory);
    }

    public void Dispose() => _loggerFactory.Dispose();

    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        // Act
        var result = _parser.Parse("");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Parse_NullString_ReturnsNull()
    {
        // Act
        var result = _parser.Parse(null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Parse_SingleInteger_ReturnsVersionWithMajorOnly()
    {
        // Act
        var result = _parser.Parse("5");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.Major);
        Assert.Equal(0, result.Minor);
    }

    [Fact]
    public void Parse_StandardSemVer_ParsesCorrectly()
    {
        // Act
        var result = _parser.Parse("1.2.3");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Major);
        Assert.Equal(2, result.Minor);
        Assert.Equal(3, result.Patch);
    }

    [Fact]
    public void Parse_TwoPartVersion_ParsesCorrectly()
    {
        // Act
        var result = _parser.Parse("v1.0");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Major);
        Assert.Equal(0, result.Minor);
    }

    [Fact]
    public void Parse_VersionWithPreRelease_ParsesCorrectly()
    {
        // Act
        var result = _parser.Parse("1.2.3-beta");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Major);
        Assert.Equal(2, result.Minor);
        Assert.Equal(3, result.Patch);
    }

    [Fact]
    public void Parse_VersionWithWildcard_TruncatesAtWildcard()
    {
        // Act
        var result = _parser.Parse("2.1.*");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Major);
        Assert.Equal(1, result.Minor);
    }

    [Fact]
    public void Parse_WithCache_ReturnsCachedVersion()
    {
        // Arrange
        var cache = new Dictionary<string, McSherry.SemanticVersioning.SemanticVersion>();
        _parser.Parse("1.2.3", cache);

        // Act
        var result = _parser.Parse("1.2.3", cache);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.Major);
        Assert.Equal(2, result.Minor);
        Assert.Equal(3, result.Patch);
        Assert.Single(cache);
    }
}
