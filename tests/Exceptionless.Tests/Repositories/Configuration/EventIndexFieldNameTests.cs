using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories.Configuration;
using Foundatio.Xunit;
using Xunit;

namespace Exceptionless.Tests.Repositories.Configuration;

public class EventIndexFieldNameTests : TestWithLoggingBase
{
    public EventIndexFieldNameTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void DataPath_EnvironmentInfoOSName_UsesJsonPropertyNameOverride()
    {
        string path = EventIndexExtensions.DataPath<EnvironmentInfo>(Event.KnownDataKeys.EnvironmentInfo, e => e.OSName);

        Assert.Equal("data.@environment.o_s_name", path);
    }

    [Fact]
    public void DataPath_RequestInfoClientIpAddress_UsesSerializerNamingPolicy()
    {
        string path = EventIndexExtensions.DataPath<RequestInfo>(Event.KnownDataKeys.RequestInfo, r => r.ClientIpAddress);

        Assert.Equal("data.@request.client_ip_address", path);
    }

    [Fact]
    public void DataDictionaryPath_RequestInfoData_AppendsKnownDictionaryKey()
    {
        string path = EventIndexExtensions.DataDictionaryPath<RequestInfo>(Event.KnownDataKeys.RequestInfo, r => r.Data, RequestInfo.KnownDataKeys.BrowserVersion);

        Assert.Equal("data.@request.data.@browser_version", path);
    }
}
