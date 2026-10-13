using System.Net;
using Exceptionless.Core.Utility;
using Xunit;

namespace Exceptionless.Tests.Utility;

public sealed class PublicAddressPolicyTests
{
    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("1.1.1.1", true)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("2001:4860:4860::8888", true)]
    [InlineData("::ffff:8.8.8.8", true)]
    [InlineData("0.0.0.0", false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("192.168.1.1", false)]
    [InlineData("192.0.0.10", false)]
    [InlineData("192.0.2.1", false)]
    [InlineData("192.88.99.1", false)]
    [InlineData("198.18.0.1", false)]
    [InlineData("198.51.100.1", false)]
    [InlineData("203.0.113.1", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("255.255.255.255", false)]
    [InlineData("::", false)]
    [InlineData("::1", false)]
    [InlineData("::ffff:192.168.1.1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("ff02::1", false)]
    [InlineData("64:ff9b::a00:1", false)]
    [InlineData("64:ff9b:1::a00:1", false)]
    [InlineData("2002:7f00:1::", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("2001::1", false)]
    [InlineData("3fff::1", false)]
    public void IsPublic_Address_RejectsSpecialUseAndTransitionRanges(string value, bool expected)
    {
        // Arrange
        var address = IPAddress.Parse(value);

        // Act
        bool result = PublicAddressPolicy.IsPublic(address);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsPublic_NullAddress_ThrowsArgumentNullException()
    {
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => PublicAddressPolicy.IsPublic(null!));

        // Assert
        Assert.Equal("address", exception.ParamName);
    }
}
