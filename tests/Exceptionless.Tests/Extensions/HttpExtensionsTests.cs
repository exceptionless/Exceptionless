using System.Text;
using Exceptionless.Web.Extensions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public sealed class HttpExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Basic")]
    [InlineData("Basic ")]
    [InlineData("Basic !!!")]
    [InlineData("Basic abc")]
    [InlineData("Basic Og==")]
    [InlineData("Basic OnBhc3N3b3Jk")]
    [InlineData("Basic ICA6cGFzc3dvcmQ=")]
    [InlineData("Bearer dXNlcjpwYXNzd29yZA==")]
    [InlineData("BasicOther dXNlcjpwYXNzd29yZA==")]
    public void GetBasicAuth_InvalidHeader_ReturnsNull(string? authorization)
    {
        // Arrange
        var request = new DefaultHttpContext().Request;
        request.Headers.Authorization = authorization;

        // Act
        var credentials = request.GetBasicAuth();

        // Assert
        Assert.Null(credentials);
    }

    [Fact]
    public void GetBasicAuth_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        HttpRequest request = null!;

        // Act
        var exception = Record.Exception(() => HttpExtensions.GetBasicAuth(request));

        // Assert
        Assert.Equal("request", Assert.IsType<ArgumentNullException>(exception).ParamName);
    }

    [Theory]
    [InlineData("Basic", "user@example.com", "password")]
    [InlineData("bAsIc", "user@example.com", "password")]
    [InlineData("Basic   ", "user@example.com", "password")]
    [InlineData("Basic", " user@example.com ", " password ")]
    [InlineData("Basic", "user@example.com", "pässwörd")]
    [InlineData("Basic", "user@example.com", "pass:word:with:colons")]
    [InlineData("Basic", "api-token", "")]
    [InlineData("Basic", "client", "api-token")]
    [InlineData("Basic", "api-token", "x-oauth-basic")]
    public void GetBasicAuth_ValidCredentials_PreservesUsernameAndPassword(string scheme, string username, string password)
    {
        // Arrange
        var request = new DefaultHttpContext().Request;
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        request.Headers.Authorization = $"{scheme} {encoded} ";

        // Act
        var credentials = request.GetBasicAuth();

        // Assert
        Assert.NotNull(credentials);
        Assert.Equal(username, credentials.Username);
        Assert.Equal(password, credentials.Password);
    }

}
