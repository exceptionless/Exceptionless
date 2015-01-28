using System;
using System.Collections.Generic;
using Exceptionless.Core;
using Exceptionless.Core.Geo;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Api.Tests.Plugins {
    public class GeoTests {
        protected readonly IGeoIPResolver _resolver;

        public GeoTests() {
            if (String.IsNullOrWhiteSpace(Settings.Current.GeoIPDatabasePath))
                return;

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