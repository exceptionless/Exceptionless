using Exceptionless.Core;
using Xunit;

namespace Exceptionless.Tests.Configuration;

public sealed class AppOptionsPushTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReadFromConfiguration_LegacyEnableWebSockets_ControlsPush(bool enabled)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BaseURL"] = "http://localhost",
            ["EnableWebSockets"] = enabled.ToString()
        }).Build();

        var options = AppOptions.ReadFromConfiguration(configuration);

        Assert.Equal(enabled, options.EnablePush);
#pragma warning disable CS0618
        Assert.Equal(enabled, options.EnableWebSockets);
#pragma warning restore CS0618
    }

    [Fact]
    public void ReadFromConfiguration_EnablePush_TakesPrecedenceOverLegacySetting()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BaseURL"] = "http://localhost",
            ["EnablePush"] = "false",
            ["EnableWebSockets"] = "true"
        }).Build();

        Assert.False(AppOptions.ReadFromConfiguration(configuration).EnablePush);
    }
}
