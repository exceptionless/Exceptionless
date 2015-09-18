using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Utility;
using Foundatio.Storage;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Api.Tests.Plugins {
    public class GeoTests {
        protected readonly IGeoIPResolver _resolver;

        public GeoTests() {
            var dataDirectory = PathHelper.ExpandPath(".\\");
            var storage = new FolderFileStorage(dataDirectory);

            if (!storage.ExistsAsync(MindMaxGeoIPResolver.GEO_IP_DATABASE_PATH).Result) {
                var job = new DownloadGeoIPDatabaseJob(storage).Run();
                Assert.NotNull(job);
                Assert.True(job.IsSuccess);
            }

            _resolver = new MindMaxGeoIPResolver(storage);
        }

        [Theory]
        [PropertyData("IPData")]
        public async Task CanResolveIp(string ip, bool canResolve) {
            if (_resolver == null)
                return;

            var result = await _resolver.ResolveIpAsync(ip).AnyContext();
            if (canResolve)
                Assert.NotNull(result);
            else
                Assert.Null(result);
        }

        [Fact]
        public async Task CanResolveIpFromCache() {
            if (_resolver == null)
                return;

            // Load the database
            await _resolver.ResolveIpAsync("0.0.0.0").AnyContext();

            var sw = new Stopwatch();
            sw.Start();
            
            for (int i = 0; i < 1000; i++)
                Assert.NotNull(await _resolver.ResolveIpAsync("8.8.4.4").AnyContext());

            sw.Stop();
            Assert.InRange(sw.ElapsedMilliseconds, 0, 65);
        }

        public static IEnumerable<object[]> IPData => new List<object[]> {
            new object[] { null, false },
            new object[] { "::1", false },
            new object[] { "127.0.0.1", false },
            new object[] { "10.0.0.0", false },
            new object[] { "172.16.0.0", false },
            new object[] { "172.31.255.255", false },
            new object[] { "192.168.0.0", false },
            new object[] { "8.8.4.4", true },
            new object[] { "2001:4860:4860::8844", true }
        }.ToArray();
    }
}