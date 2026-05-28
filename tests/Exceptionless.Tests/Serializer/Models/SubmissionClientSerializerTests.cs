using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public class SubmissionClientSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public SubmissionClientSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void RoundTrip_WithAllProperties_PreservesValues()
    {
        // Arrange
        var client = new SubmissionClient
        {
            IpAddress = "192.168.1.100",
            UserAgent = "exceptionless/1.0.0",
            Version = "2.1.3"
        };

        // Act
        string? json = _serializer.SerializeToString(client);
        var result = _serializer.Deserialize<SubmissionClient>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("192.168.1.100", result.IpAddress);
        Assert.Equal("exceptionless/1.0.0", result.UserAgent);
        Assert.Equal("2.1.3", result.Version);
    }

    [Fact]
    public void RoundTrip_WithIPv6Address_PreservesValues()
    {
        // Arrange
        var client = new SubmissionClient
        {
            IpAddress = "::ffff:192.168.1.1",
            UserAgent = "exceptionless-js/3.0.0",
            Version = "3.0.0"
        };

        // Act
        string? json = _serializer.SerializeToString(client);
        var result = _serializer.Deserialize<SubmissionClient>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("::ffff:192.168.1.1", result.IpAddress);
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesCorrectly()
    {
        // Arrange
        /* language=json */
        const string json = """{"ip_address":"10.0.0.1","user_agent":"Mozilla/5.0","version":"1.0.0"}""";

        // Act
        var result = _serializer.Deserialize<SubmissionClient>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("10.0.0.1", result.IpAddress);
        Assert.Equal("Mozilla/5.0", result.UserAgent);
        Assert.Equal("1.0.0", result.Version);
    }

    [Fact]
    public void RoundTrip_WithMinimalProperties_PreservesValues()
    {
        // Arrange
        var client = new SubmissionClient { IpAddress = "127.0.0.1" };

        // Act
        string? json = _serializer.SerializeToString(client);
        var result = _serializer.Deserialize<SubmissionClient>(json);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("127.0.0.1", result.IpAddress);
        Assert.Null(result.UserAgent);
    }
}
