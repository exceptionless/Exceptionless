using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

/// <summary>
/// Tests RequestInfo serialization through ITextSerializer.
/// Validates round-trip serialization with snake_case property naming.
/// </summary>
public class RequestInfoSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public RequestInfoSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void SerializeToString_CompleteRequestInfo_PreservesAllProperties()
    {
        // Arrange
        var request = new RequestInfo
        {
            HttpMethod = "POST",
            Path = "/api/v1/events",
            Host = "api.example.com",
            Port = 443,
            IsSecure = true,
            ClientIpAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"
        };

        // Act
        string? json = _serializer.SerializeToString(request);
        var deserialized = _serializer.Deserialize<RequestInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("POST", deserialized.HttpMethod);
        Assert.Equal("/api/v1/events", deserialized.Path);
        Assert.Equal("api.example.com", deserialized.Host);
        Assert.Equal(443, deserialized.Port);
        Assert.True(deserialized.IsSecure);
        Assert.Equal("192.168.1.100", deserialized.ClientIpAddress);
    }

    [Fact]
    public void SerializeToString_MinimalRequestInfo_PreservesProperties()
    {
        // Arrange
        var request = new RequestInfo
        {
            HttpMethod = "GET",
            Path = "/health"
        };

        // Act
        string? json = _serializer.SerializeToString(request);
        var deserialized = _serializer.Deserialize<RequestInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("GET", deserialized.HttpMethod);
        Assert.Equal("/health", deserialized.Path);
    }

    [Fact]
    public void Deserialize_CompleteRequestInfo_PreservesAllProperties()
    {
        // Arrange
        var original = new RequestInfo
        {
            HttpMethod = "PUT",
            Path = "/api/users/123",
            Host = "localhost",
            Port = 5000,
            IsSecure = false,
            ClientIpAddress = "127.0.0.1",
            UserAgent = "TestClient/1.0"
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<RequestInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("PUT", deserialized.HttpMethod);
        Assert.Equal("/api/users/123", deserialized.Path);
        Assert.Equal("localhost", deserialized.Host);
        Assert.Equal(5000, deserialized.Port);
        Assert.False(deserialized.IsSecure);
        Assert.Equal("127.0.0.1", deserialized.ClientIpAddress);
        Assert.Equal("TestClient/1.0", deserialized.UserAgent);
    }

    [Fact]
    public void Deserialize_RequestInfoWithQueryString_PreservesQueryString()
    {
        // Arrange
        var original = new RequestInfo
        {
            HttpMethod = "GET",
            Path = "/search",
            QueryString = new Dictionary<string, string>
            {
                ["q"] = "test query",
                ["page"] = "1",
                ["limit"] = "10"
            }
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<RequestInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.QueryString);
        Assert.Equal("test query", deserialized.QueryString["q"]);
        Assert.Equal("1", deserialized.QueryString["page"]);
    }

    [Fact]
    public void Deserialize_RequestInfoWithCookies_PreservesCookies()
    {
        // Arrange
        var original = new RequestInfo
        {
            HttpMethod = "GET",
            Path = "/dashboard",
            Cookies = new Dictionary<string, string>
            {
                ["session_id"] = "abc123",
                ["theme"] = "dark"
            }
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<RequestInfo>(json);

        // Assert
        Assert.NotNull(deserialized?.Cookies);
        Assert.Equal("abc123", deserialized.Cookies["session_id"]);
        Assert.Equal("dark", deserialized.Cookies["theme"]);
    }

    [Fact]
    public void Deserialize_RequestInfoWithPostData_PreservesPostData()
    {
        // Arrange
        var original = new RequestInfo
        {
            HttpMethod = "POST",
            Path = "/api/login",
            PostData = new Dictionary<string, string>
            {
                ["username"] = "testuser",
                ["remember_me"] = "true"
            }
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<RequestInfo>(json);

        // Assert
        Assert.NotNull(deserialized?.PostData);
        var postData = deserialized.PostData as IDictionary<string, object>;
        Assert.NotNull(postData);
        Assert.Equal("testuser", postData["username"]);
        Assert.Equal("true", postData["remember_me"]);
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesAllProperties()
    {
        // Arrange
        /* language=json */
        const string json = """{"user_agent":"Chrome/100","http_method":"DELETE","is_secure":true,"host":"api.test.com","port":8443,"path":"/api/items/456","client_ip_address":"10.0.0.1"}""";

        // Act
        var request = _serializer.Deserialize<RequestInfo>(json);

        // Assert
        Assert.NotNull(request);
        Assert.Equal("DELETE", request.HttpMethod);
        Assert.Equal("/api/items/456", request.Path);
        Assert.Equal("api.test.com", request.Host);
        Assert.Equal(8443, request.Port);
        Assert.True(request.IsSecure);
    }

    [Fact]
    public void Deserialize_RequestInfoWithSpecialCharactersInPath_PreservesPath()
    {
        // Arrange
        var original = new RequestInfo
        {
            HttpMethod = "GET",
            Path = "/api/files/path%2Fto%2Ffile.txt"
        };

        // Act
        string? json = _serializer.SerializeToString(original);
        var deserialized = _serializer.Deserialize<RequestInfo>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("/api/files/path%2Fto%2Ffile.txt", deserialized.Path);
    }
}
