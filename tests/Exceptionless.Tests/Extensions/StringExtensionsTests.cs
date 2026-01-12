using Exceptionless.Core.Extensions;
using Xunit;

namespace Exceptionless.Tests.Extensions;

public class StringExtensionsTests : TestWithServices
{
    public StringExtensionsTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ToAddress_VariousFormats_ExtractsAddressCorrectly()
    {
        Assert.Equal("::1", "::1".ToAddress());
        Assert.Equal("1.2.3.4", "1.2.3.4".ToAddress());
        Assert.Equal("1.2.3.4", "1.2.3.4:".ToAddress());
        Assert.Equal("1.2.3.4", "1.2.3.4:80".ToAddress());
        Assert.Equal("1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7:8".ToAddress());
        Assert.Equal("1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7:8:".ToAddress());
        Assert.Equal("1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7:8:80".ToAddress());
    }

    /// <summary>
    /// Tests the ToLowerUnderscoredWords extension method behavior.
    /// Note: Each uppercase letter gets an underscore before it (except at position 0),
    /// so "EnableSSL" becomes "enable_s_s_l" - this is the established API contract.
    /// </summary>
    [Theory]
    // Realistic app config properties
    [InlineData("BaseURL", "base_u_r_l")]                           // AppOptions property
    [InlineData("EnableSSL", "enable_s_s_l")]                       // EmailOptions property
    [InlineData("IPAddress", "i_p_address")]                        // Environment property
    [InlineData("OSName", "o_s_name")]                              // Environment property (from event-serialization-input.json)
    [InlineData("OSVersion", "o_s_version")]                        // Environment property
    // Standard PascalCase
    [InlineData("WebsiteMode", "website_mode")]                     // AppOptions property
    [InlineData("MaximumRetentionDays", "maximum_retention_days")]  // AppOptions property
    [InlineData("SmtpHost", "smtp_host")]                           // EmailOptions property
    // Elasticsearch special cases (must be preserved)
    [InlineData("_type", "_type")]                                  // Leading underscore preserved
    [InlineData("__type", "__type")]                                // Double leading underscore preserved
    // Already lowercase with underscores - no change
    [InlineData("ip_address", "ip_address")]                        // Already snake_case
    [InlineData("o_s_name", "o_s_name")]                            // Already snake_case
    // Dots and special characters preserved
    [InlineData("node.data", "node.data")]                          // Elasticsearch field path
    [InlineData("127.0.0.1", "127.0.0.1")]                          // IP address literal
    // Edge cases
    [InlineData("", "")]                                            // Empty string
    [InlineData("Id", "id")]                                        // Single word
    public void ToLowerUnderscoredWords_VariousInputFormats_ReturnsSnakeCase(string input, string expected)
    {
        Assert.Equal(expected, input.ToLowerUnderscoredWords());
    }
}
