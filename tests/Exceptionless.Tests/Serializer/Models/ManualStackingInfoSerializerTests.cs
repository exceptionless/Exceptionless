using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Serializer.Models;

public class ManualStackingInfoSerializerTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public ManualStackingInfoSerializerTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void RoundTrip_WithAllProperties_PreservesValues()
    {
        var info = new ManualStackingInfo
        {
            Title = "Payment Processing Error",
            SignatureData = new Dictionary<string, string>
            {
                ["payment_provider"] = "stripe",
                ["error_code"] = "card_declined",
                ["region"] = "US"
            }
        };

        string? json = _serializer.SerializeToString(info);
        var result = _serializer.Deserialize<ManualStackingInfo>(json);

        Assert.NotNull(result);
        Assert.Equal("Payment Processing Error", result.Title);
        Assert.NotNull(result.SignatureData);
        Assert.Equal(3, result.SignatureData.Count);
        Assert.Equal("stripe", result.SignatureData["payment_provider"]);
        Assert.Equal("card_declined", result.SignatureData["error_code"]);
    }

    [Fact]
    public void Deserialize_SnakeCaseJson_ParsesCorrectly()
    {
        /* language=json */
        const string json = """{"title":"Custom Stack","signature_data":{"key":"value"}}""";

        var result = _serializer.Deserialize<ManualStackingInfo>(json);

        Assert.NotNull(result);
        Assert.Equal("Custom Stack", result.Title);
        Assert.NotNull(result.SignatureData);
        Assert.Equal("value", result.SignatureData["key"]);
    }

    [Fact]
    public void RoundTrip_WithSpecialCharacters_PreservesValues()
    {
        var info = new ManualStackingInfo
        {
            Title = "Error: \"Connection refused\" at /api/v2/events",
            SignatureData = new Dictionary<string, string>
            {
                ["path"] = "/api/v2/events?filter=type:error",
                ["message"] = "Connection refused: host=db.example.com, port=5432"
            }
        };

        string? json = _serializer.SerializeToString(info);
        var result = _serializer.Deserialize<ManualStackingInfo>(json);

        Assert.NotNull(result);
        Assert.Contains("Connection refused", result.Title);
        Assert.Equal("/api/v2/events?filter=type:error", result.SignatureData!["path"]);
    }

    [Fact]
    public void RoundTrip_WithMinimalProperties_PreservesValues()
    {
        var info = new ManualStackingInfo { Title = "Simple Stack" };

        string? json = _serializer.SerializeToString(info);
        var result = _serializer.Deserialize<ManualStackingInfo>(json);

        Assert.NotNull(result);
        Assert.Equal("Simple Stack", result.Title);
    }

    [Fact]
    public void DataDictionary_GetValue_ManualStackingInfo_FromDictionary()
    {
        var dict = new DataDictionary
        {
            ["@stack"] = new ManualStackingInfo
            {
                Title = "Custom",
                SignatureData = new Dictionary<string, string> { ["key"] = "val" }
            }
        };

        var result = dict.GetValue<ManualStackingInfo>("@stack", _serializer);

        Assert.NotNull(result);
        Assert.Equal("Custom", result.Title);
        Assert.NotNull(result.SignatureData);
        Assert.Equal("val", result.SignatureData["key"]);
    }
}
