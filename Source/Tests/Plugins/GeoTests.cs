using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.EventProcessor.Default;
using Exceptionless.Core.Utility;
using Foundatio.Caching;
using Foundatio.Storage;
using Xunit;

namespace Exceptionless.Api.Tests.Plugins {
    public class GeoTests {
        private const string GREEN_BAY_COORDINATES = "44.5241,-87.9056";
        private const string GREEN_BAY_IP = "143.200.133.1";
        private const string IRVING_COORDINATES = "32.85,-96.9613";
        private const string IRVING_IP = "192.91.253.248";
        private readonly IGeocodeService _geocodeService = IoC.GetInstance<IGeocodeService>();

        private static IGeoIpService _service;
        private static async Task<IGeoIpService> GetResolverAsync() {
            if (_service != null)
                return _service;

            var dataDirectory = PathHelper.ExpandPath(".\\");
            var storage = new FolderFileStorage(dataDirectory);

            if (!await storage.ExistsAsync(MaxMindGeoIpService.GEO_IP_DATABASE_PATH)) {
                var job = new DownloadGeoIPDatabaseJob(new InMemoryCacheClient(), storage);
                var result = await job.RunAsync();
                Assert.NotNull(result);
                Assert.True(result.IsSuccess);
            }

            return _service = new MaxMindGeoIpService(storage);
        }
        
        [Fact]
        public async Task WillNotSetLocation() {
            var plugin = new GeoPlugin(await GetResolverAsync());
            var ev = new PersistentEvent { Geo = GREEN_BAY_COORDINATES };
            await plugin.EventBatchProcessingAsync(new List<EventContext> { new EventContext(ev) });

            Assert.Equal(GREEN_BAY_COORDINATES, ev.Geo);
            Assert.Null(ev.GetLocation());
        }
       
        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("Invalid")]
        [InlineData("x,y")]
        [InlineData("190,180")]
        public async Task WillResetLocation(string geo) {
            var plugin = new GeoPlugin(await GetResolverAsync());
            
            var ev = new PersistentEvent { Geo = geo };
            await plugin.EventBatchProcessingAsync(new List<EventContext> { new EventContext(ev) });

            Assert.Null(ev.Geo);
            Assert.Null(ev.GetLocation());
        }
        
        [Fact]
        public async Task WillSetLocationFromGeo() {
            var plugin = new GeoPlugin(await GetResolverAsync());
            var ev = new PersistentEvent { Geo = GREEN_BAY_IP };
            await plugin.EventBatchProcessingAsync(new List<EventContext> { new EventContext(ev) });

            Assert.NotNull(ev.Geo);
            Assert.NotEqual(GREEN_BAY_IP, ev.Geo);

            var location = ev.GetLocation();
            Assert.Equal("US", location?.Country);
            Assert.Equal("WI", location?.Level1);
            Assert.Equal("Green Bay", location?.Locality);
        }

        [Fact]
        public async Task WillSetLocationFromRequestInfo() {
            var plugin = new GeoPlugin(await GetResolverAsync());
            var ev = new PersistentEvent();
            ev.AddRequestInfo(new RequestInfo { ClientIpAddress = GREEN_BAY_IP });
            await plugin.EventBatchProcessingAsync(new List<EventContext> { new EventContext(ev) });

            Assert.NotNull(ev.Geo);

            var location = ev.GetLocation();
            Assert.Equal("US", location?.Country);
            Assert.Equal("WI", location?.Level1);
            Assert.Equal("Green Bay", location?.Locality);
        }

        [Fact]
        public async Task WillSetLocationFromEnvironmentInfoInfo() {
            var plugin = new GeoPlugin(await GetResolverAsync());
            var ev = new PersistentEvent();
            ev.SetEnvironmentInfo(new EnvironmentInfo { IpAddress = $"127.0.0.1,{GREEN_BAY_IP}" });
            await plugin.EventBatchProcessingAsync(new List<EventContext> { new EventContext(ev) });

            Assert.NotNull(ev.Geo);

            var location = ev.GetLocation();
            Assert.Equal("US", location?.Country);
            Assert.Equal("WI", location?.Level1);
            Assert.Equal("Green Bay", location?.Locality);
        }

        [Fact]
        public async Task WillSetFromSingleGeo() {
            var plugin = new GeoPlugin(await GetResolverAsync());

            var contexts = new List<EventContext> {
                new EventContext(new PersistentEvent { Geo = GREEN_BAY_IP }),
                new EventContext(new PersistentEvent { Geo = GREEN_BAY_IP })
            };

            await plugin.EventBatchProcessingAsync(contexts);

            foreach (var context in contexts) {
                Assert.Equal(GREEN_BAY_COORDINATES, context.Event.Geo);

                var location = context.Event.GetLocation();
                Assert.Equal("US", location?.Country);
                Assert.Equal("WI", location?.Level1);
                Assert.Equal("Green Bay", location?.Locality);
            }
        }
        
        [Fact]
        public async Task WillNotSetFromMultipleGeo() {
            var plugin = new GeoPlugin(await GetResolverAsync());

            var ev = new PersistentEvent();
            var greenBayEvent = new PersistentEvent { Geo = GREEN_BAY_IP };
            var irvingEvent = new PersistentEvent { Geo = IRVING_IP };
            await plugin.EventBatchProcessingAsync(new List<EventContext> {
                new EventContext(ev),
                new EventContext(greenBayEvent),
                new EventContext(irvingEvent)
            });

            Assert.Equal(GREEN_BAY_COORDINATES, greenBayEvent.Geo);
            var location = greenBayEvent.GetLocation();
            Assert.Equal("US", location?.Country);
            Assert.Equal("WI", location?.Level1);
            Assert.Equal("Green Bay", location?.Locality);

            Assert.Equal(IRVING_COORDINATES, irvingEvent.Geo);
            location = irvingEvent.GetLocation();
            Assert.Equal("US", location?.Country);
            Assert.Equal("TX", location?.Level1);
            Assert.Equal("Irving", location?.Locality);
        }

        [Fact]
        public async Task ReverseGeocodeLookup() {
            if (_geocodeService is NullGeocodeService)
                return;

            GeoResult coordinates;
            Assert.True(GeoResult.TryParse(GREEN_BAY_COORDINATES, out coordinates));
            var location = await _geocodeService.ReverseGeocodeAsync(coordinates.Latitude.GetValueOrDefault(), coordinates.Longitude.GetValueOrDefault());
            Assert.Equal("US", location?.Country);
            Assert.Equal("WI", location?.Level1);
            Assert.Equal("Brown County", location?.Level2);
            Assert.Equal("Green Bay", location?.Locality);
        }

        [Fact]
        public async Task WillSetMultipleFromEmptyGeo() {
            var plugin = new GeoPlugin(await GetResolverAsync());

            var ev = new PersistentEvent();
            var greenBayEvent = new PersistentEvent();
            greenBayEvent.SetEnvironmentInfo(new EnvironmentInfo { IpAddress = GREEN_BAY_IP });
            var irvingEvent = new PersistentEvent();
            irvingEvent.SetEnvironmentInfo(new EnvironmentInfo { IpAddress = IRVING_IP });
            await plugin.EventBatchProcessingAsync(new List<EventContext> {
                new EventContext(ev),
                new EventContext(greenBayEvent),
                new EventContext(irvingEvent)
            });

            Assert.Equal(GREEN_BAY_COORDINATES, greenBayEvent.Geo);
            var location = greenBayEvent.GetLocation();
            Assert.Equal("US", location?.Country);
            Assert.Equal("WI", location?.Level1);
            Assert.Equal("Green Bay", location?.Locality);

            Assert.Equal(IRVING_COORDINATES, irvingEvent.Geo);
            location = irvingEvent.GetLocation();
            Assert.Equal("US", location?.Country);
            Assert.Equal("TX", location?.Level1);
            Assert.Equal("Irving", location?.Locality);
        }

        [Theory]
        [MemberData("IPData")]
        public async Task CanResolveIpAsync(string ip, bool canResolve) {
            var resolver = await GetResolverAsync();
            var result = await resolver.ResolveIpAsync(ip);
            if (canResolve)
                Assert.NotNull(result);
            else
                Assert.Null(result);
        }

        [Fact]
        public async Task CanResolveIpFromCacheAsync() {
            var resolver = await GetResolverAsync();

            // Load the database
            await resolver.ResolveIpAsync("0.0.0.0");

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
                Assert.NotNull(await resolver.ResolveIpAsync("8.8.4.4"));

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