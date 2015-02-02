using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Exceptionless.Core;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Utility;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Api.Tests.Plugins {
    public class GeoTests {
        protected readonly IGeoIPResolver _resolver;

        public GeoTests() {
            var databasePath = PathHelper.ExpandPath(Settings.Current.GeoIPDatabasePath);
            if (String.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath)) {
                Console.WriteLine("Unable to resolve GeoIP Database Path");
                return;
            }

            _resolver = new MindMaxGeoIPResolver();
        }

        [Theory]
        [PropertyData("IPData")]
        public void CanResolveIp(string ip, bool canResolve) {
            if (_resolver == null)
                return;

            var result = _resolver.ResolveIp(ip);
            if (canResolve)
                Assert.NotNull(result);
            else
                Assert.Null(result);
        }

        [Fact]
        public void CanResolveIpFromCache() {
            if (_resolver == null)
                return;

            var sw = new Stopwatch();
            sw.Start();
            
            for (int i = 0; i < 1000; i++)
                Assert.NotNull(_resolver.ResolveIp("8.8.4.4"));

            sw.Stop();
            Assert.InRange(sw.ElapsedMilliseconds, 0, 510);
        }

        public static IEnumerable<object[]> IPData {
            get {
                return new List<object[]> {
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
    }
}