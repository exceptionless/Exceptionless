using Exceptionless.Core.Extensions;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Utility;

public class StringExtensionsTests : TestWithLoggingBase
{
    public StringExtensionsTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void AnyWildcardMatches_EndsWithWildcard_MatchesPrefix()
    {
        // Arrange
        var patterns = new[] { "hello*" };

        // Act
        bool result = "hello world".AnyWildcardMatches(patterns);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AnyWildcardMatches_ExactPattern_MatchesExactValue()
    {
        // Arrange
        var patterns = new[] { "hello" };

        // Act
        bool result = "hello".AnyWildcardMatches(patterns);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AnyWildcardMatches_ExactPattern_DoesNotMatchDifferentValue()
    {
        // Arrange
        var patterns = new[] { "hello" };

        // Act
        bool result = "world".AnyWildcardMatches(patterns);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AnyWildcardMatches_IgnoreCase_MatchesCaseInsensitive()
    {
        // Arrange
        var patterns = new[] { "HELLO*" };

        // Act
        bool result = "hello world".AnyWildcardMatches(patterns, ignoreCase: true);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AnyWildcardMatches_MultiplePatterns_MatchesAny()
    {
        // Arrange
        var patterns = new[] { "foo*", "bar*" };

        // Act
        bool result = "bar baz".AnyWildcardMatches(patterns);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AnyWildcardMatches_StartsWithWildcard_MatchesSuffix()
    {
        // Arrange
        var patterns = new[] { "*world" };

        // Act
        bool result = "hello world".AnyWildcardMatches(patterns);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AnyWildcardMatches_SurroundingWildcards_MatchesContains()
    {
        // Arrange
        var patterns = new[] { "*llo wor*" };

        // Act
        bool result = "hello world".AnyWildcardMatches(patterns);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsLocalHost_Ipv4Loopback_ReturnsTrue()
    {
        // Act
        bool result = "127.0.0.1".IsLocalHost();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsLocalHost_Ipv6Loopback_ReturnsTrue()
    {
        // Act
        bool result = "::1".IsLocalHost();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsLocalHost_NullInput_ReturnsFalse()
    {
        // Act
        bool result = ((string?)null).IsLocalHost();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLocalHost_EmptyString_ReturnsFalse()
    {
        // Act
        bool result = "".IsLocalHost();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsLocalHost_PublicIp_ReturnsFalse()
    {
        // Act
        bool result = "8.8.8.8".IsLocalHost();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsNumeric_EmptyString_ReturnsFalse()
    {
        // Act
        bool result = "".IsNumeric();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsNumeric_NegativeNumber_ReturnsTrue()
    {
        // Act
        bool result = "-42".IsNumeric();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsNumeric_NullInput_ReturnsFalse()
    {
        // Act
        bool result = ((string?)null).IsNumeric();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsNumeric_PositiveNumber_ReturnsTrue()
    {
        // Act
        bool result = "12345".IsNumeric();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsNumeric_StringWithLetters_ReturnsFalse()
    {
        // Act
        bool result = "123abc".IsNumeric();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsNumeric_StringWithDecimalPoint_ReturnsFalse()
    {
        // Act
        bool result = "3.14".IsNumeric();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPrivateNetwork_ClassAAddress_ReturnsTrue()
    {
        // Act
        bool result = "10.0.0.1".IsPrivateNetwork();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPrivateNetwork_ClassBAddress_ReturnsTrue()
    {
        // Act
        bool result = "172.16.0.1".IsPrivateNetwork();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPrivateNetwork_ClassBUpperBound_ReturnsTrue()
    {
        // Act
        bool result = "172.31.255.255".IsPrivateNetwork();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPrivateNetwork_ClassBOutOfRange_ReturnsFalse()
    {
        // Act
        bool result = "172.32.0.1".IsPrivateNetwork();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPrivateNetwork_ClassCAddress_ReturnsTrue()
    {
        // Act
        bool result = "192.168.1.1".IsPrivateNetwork();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPrivateNetwork_EmptyString_ReturnsFalse()
    {
        // Act
        bool result = "".IsPrivateNetwork();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPrivateNetwork_Localhost_ReturnsTrue()
    {
        // Act
        bool result = "127.0.0.1".IsPrivateNetwork();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPrivateNetwork_NullInput_ReturnsFalse()
    {
        // Act
        bool result = ((string?)null).IsPrivateNetwork();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPrivateNetwork_PublicIp_ReturnsFalse()
    {
        // Act
        bool result = "8.8.8.8".IsPrivateNetwork();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidFieldName_NullInput_ReturnsFalse()
    {
        // Act
        bool result = ((string?)null).IsValidFieldName();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidFieldName_Over25Characters_ReturnsFalse()
    {
        // Act
        bool result = "abcdefghijklmnopqrstuvwxyz".IsValidFieldName();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidFieldName_ValidShortName_ReturnsTrue()
    {
        // Act
        bool result = "my-field".IsValidFieldName();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidFieldName_WithSpecialChars_ReturnsFalse()
    {
        // Act
        bool result = "my_field".IsValidFieldName();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidIdentifier_AlphanumericWithDash_ReturnsTrue()
    {
        // Act
        bool result = "my-identifier-123".IsValidIdentifier();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidIdentifier_EmptyString_ReturnsTrue()
    {
        // Act
        bool result = "".IsValidIdentifier();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidIdentifier_NullInput_ReturnsFalse()
    {
        // Act
        bool result = ((string?)null).IsValidIdentifier();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidIdentifier_WithUnderscore_ReturnsFalse()
    {
        // Act
        bool result = "has_underscore".IsValidIdentifier();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidIdentifier_WithSpaces_ReturnsFalse()
    {
        // Act
        bool result = "has spaces".IsValidIdentifier();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ToAddress_EmptyString_ReturnsEmpty()
    {
        // Act
        string result = "".ToAddress();

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void ToAddress_Ipv4WithPort_StripsPort()
    {
        // Act
        string result = "1.2.3.4:80".ToAddress();

        // Assert
        Assert.Equal("1.2.3.4", result);
    }

    [Fact]
    public void ToAddress_Ipv4WithoutPort_ReturnsUnchanged()
    {
        // Act
        string result = "1.2.3.4".ToAddress();

        // Assert
        Assert.Equal("1.2.3.4", result);
    }

    [Fact]
    public void ToAddress_Ipv6Full_ReturnsUnchanged()
    {
        // Act
        string result = "1:2:3:4:5:6:7:8".ToAddress();

        // Assert
        Assert.Equal("1:2:3:4:5:6:7:8", result);
    }

    [Fact]
    public void ToAddress_Ipv6Loopback_ReturnsUnchanged()
    {
        // Act
        string result = "::1".ToAddress();

        // Assert
        Assert.Equal("::1", result);
    }

    [Fact]
    public void ToAddress_Ipv6WithPort_StripsPort()
    {
        // Act
        string result = "1:2:3:4:5:6:7:8:80".ToAddress();

        // Assert
        Assert.Equal("1:2:3:4:5:6:7:8", result);
    }
}
