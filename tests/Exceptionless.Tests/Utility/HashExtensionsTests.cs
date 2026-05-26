using Exceptionless.Core.Extensions;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Utility;

public class HashExtensionsTests : TestWithLoggingBase
{
    public HashExtensionsTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ToHex_EmptyByteArray_ReturnsEmptyString()
    {
        // Arrange
        byte[] bytes = [];

        // Act
        string result = bytes.ToHex();

        // Assert
        Assert.Equal("", result);
    }

    [Fact]
    public void ToHex_KnownBytes_ReturnsLowercaseHex()
    {
        // Arrange
        byte[] bytes = [0xDE, 0xAD, 0xBE, 0xEF];

        // Act
        string result = bytes.ToHex();

        // Assert
        Assert.Equal("deadbeef", result);
    }

    [Fact]
    public void ToHex_LeadingZeroByte_PadsWithZero()
    {
        // Arrange
        byte[] bytes = [0x00, 0xFF, 0x10];

        // Act
        string result = bytes.ToHex();

        // Assert
        Assert.Equal("00ff10", result);
    }

    [Fact]
    public void ToSHA1_DeterministicOutput_ReturnsSameHashForSameInput()
    {
        // Act
        string hash1 = "hello".ToSHA1();
        string hash2 = "hello".ToSHA1();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ToSHA1_DifferentInputs_ReturnsDifferentHashes()
    {
        // Act
        string hash1 = "hello".ToSHA1();
        string hash2 = "world".ToSHA1();

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ToSHA1_KnownInput_ReturnsExpectedHash()
    {
        // Act
        string result = "hello".ToSHA1();

        // Assert (SHA1 of "hello" encoded as UTF-16LE)
        Assert.Equal("b6d795fbd58cc7592d955a219374339a323801a9", result);
    }

    [Fact]
    public void ToSHA1_KnownInputTest_ReturnsExpectedHash()
    {
        // Act
        string result = "test".ToSHA1();

        // Assert (SHA1 of "test" encoded as UTF-16LE)
        Assert.Equal("87f8ed9157125ffc4da9e06a7b8011ad80a53fe1", result);
    }

    [Fact]
    public void ToSHA256_DeterministicOutput_ReturnsSameHashForSameInput()
    {
        // Act
        string hash1 = "hello".ToSHA256();
        string hash2 = "hello".ToSHA256();

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ToSHA256_KnownInput_ReturnsExpectedHash()
    {
        // Act
        string result = "hello".ToSHA256();

        // Assert (SHA256 of "hello" encoded as UTF-16LE)
        Assert.Equal("06e44dc1b95c469f43aaccb49e93c36827626266eed5575eced74af9a016c9cd", result);
    }

    [Fact]
    public void ToSHA256_KnownInputTest_ReturnsExpectedHash()
    {
        // Act
        string result = "test".ToSHA256();

        // Assert (SHA256 of "test" encoded as UTF-16LE)
        Assert.Equal("fe520676b1a1d93dabab2319eea03674f3632eaeeb163d1e88244f5eb1de10eb", result);
    }

    [Fact]
    public void ToSHA256_ProducesLongerHashThanSHA1()
    {
        // Act
        string sha1 = "hello".ToSHA1();
        string sha256 = "hello".ToSHA256();

        // Assert (SHA1 = 40 hex chars, SHA256 = 64 hex chars)
        Assert.Equal(40, sha1.Length);
        Assert.Equal(64, sha256.Length);
    }
}
