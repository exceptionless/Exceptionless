using System;
using Exceptionless.Core.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Extensions {
    public class StringExtensionsTests : TestBase {
        public StringExtensionsTests(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void ToAddress() {
            Assert.Equal("::1", "::1".ToAddress());
            Assert.Equal("1.2.3.4", "1.2.3.4".ToAddress());
            Assert.Equal("1.2.3.4", "1.2.3.4:".ToAddress());
            Assert.Equal("1.2.3.4", "1.2.3.4:80".ToAddress());
            Assert.Equal("1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7:8".ToAddress());
            Assert.Equal("1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7:8:".ToAddress());
            Assert.Equal("1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7:8:80".ToAddress());
        }

        [Fact(Skip = "TODO: https://github.com/exceptionless/Exceptionless.Net/issues/2")]
        public void LowerUnderscoredWords() {
            Assert.Equal("enable_ssl", "EnableSSL".ToLowerUnderscoredWords());
            Assert.Equal("base_url", "BaseURL".ToLowerUnderscoredWords());
            Assert.Equal("website_mode", "WebsiteMode".ToLowerUnderscoredWords());
            Assert.Equal("google_app_id", "GoogleAppId".ToLowerUnderscoredWords());

            Assert.Equal("blake_niemyjski_1", "blakeNiemyjski 1".ToLowerUnderscoredWords());
            Assert.Equal("blake_niemyjski_2", "Blake     Niemyjski 2".ToLowerUnderscoredWords());
            Assert.Equal("blake_niemyjski_3", "Blake_ niemyjski 3".ToLowerUnderscoredWords());
            Assert.Equal("blake_niemyjski4", "Blake_Niemyjski4".ToLowerUnderscoredWords());
            Assert.Equal("mp3_files_data", "MP3FilesData".ToLowerUnderscoredWords());
            Assert.Equal("flac", "FLAC".ToLowerUnderscoredWords());
            Assert.Equal("number_of_abcd_things", "NumberOfABCDThings".ToLowerUnderscoredWords());
            Assert.Equal("ip_address_2s", "IPAddress 2s".ToLowerUnderscoredWords());
            Assert.Equal("127.0.0.1", "127.0.0.1".ToLowerUnderscoredWords());
            Assert.Equal("", "".ToLowerUnderscoredWords());
            Assert.Equal("_type", "_type".ToLowerUnderscoredWords());
            Assert.Equal("__type", "__type".ToLowerUnderscoredWords());
            Assert.Equal("my_custom_type", "myCustom   _type".ToLowerUnderscoredWords());
            Assert.Equal("my_custom_type", "myCustom_type".ToLowerUnderscoredWords());
            Assert.Equal("my_custom_type", "myCustom _type".ToLowerUnderscoredWords());
            Assert.Equal("node.data", "node.data".ToLowerUnderscoredWords());
            Assert.Equal("match_mapping_type", "match_mapping_type".ToLowerUnderscoredWords());
        }
    }
}
