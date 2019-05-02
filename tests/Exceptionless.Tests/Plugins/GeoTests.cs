using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.EventProcessor.Default;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Foundatio.Caching;
using Foundatio.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Plugins {
    public class GeoTests : TestWithServices {
        private const string GREEN_BAY_COORDINATES = "44.5458,-88.1019";
        private const string GREEN_BAY_IP = "24.208.86.80";
        private const string IRVING_COORDINATES = "32.8489,-96.9667";
        private const string IRVING_IP = "192.91.253.248";
        private readonly BillingManager _billingManager;
        private readonly BillingPlans _plans;
        private readonly IOptions<AppOptions> _options;

        public GeoTests(ServicesFixture fixture, ITestOutputHelper output) : base(fixture, output) {
            _billingManager = GetService<BillingManager>();
            _plans = GetService<BillingPlans>();
            _options = GetService<IOptions<AppOptions>>();
        }

        private async Task<IGeoIpService> GetResolverAsync(ILoggerFactory loggerFactory) {
            string dataDirectory = PathHelper.ExpandPath(".\\");
            var storage = new FolderFileStorage(new FolderFileStorageOptions {
                Folder = dataDirectory,
                LoggerFactory = loggerFactory
            });

            if (!await storage.ExistsAsync(MaxMindGeoIpService.GEO_IP_DATABASE_PATH)) {
                var job = new DownloadGeoIPDatabaseJob(GetService<ICacheClient>(), storage, loggerFactory);
                var result = await job.RunAsync();
                Assert.NotNull(result);
                Assert.True(result.IsSuccess);
            }

            return new MaxMindGeoIpService(storage, loggerFactory);
        }

        [Fact]
        public async Task WillNotSetLocation() {
            var plugin = new GeoPlugin(await GetResolverAsync(Log), _options);
            var ev = new PersistentEvent { Geo = GREEN_BAY_COORDINATES };
            await plugin.EventBatchProcessingAsync(new List<EventContext> { new EventContext(ev, OrganizationData.GenerateSampleOrganization(_billingManager, _plans), ProjectData.GenerateSampleProject()) });

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
            var plugin = new GeoPlugin(await GetResolverAsync(Log), _options);

            var ev = new PersistentEvent { Geo = geo };
            await plugin.EventBatchProcessingAsync(new List<EventContext> { new EventContext(ev, OrganizationData.GenerateSampleOrganization(_billingManager, _plans), ProjectData.GenerateSampleProject()) });

            Assert.Null(ev.Geo);
            Assert.Null(ev.GetLocation());
        }

        [Fact]
        public async Task WillSetLocationFromGeo() {
            var plugin = new GeoPlugin(await GetResolverAsync(Log), _options);
            var ev = new PersistentEvent { Geo = GREEN_BAY_IP };
            await plugin.EventBatchProcessingAsync(new List<EventContext> { new EventContext(ev, OrganizationData.GenerateSampleOrganization(_billingManager, _plans), ProjectData.GenerateSampleProject()) });

            Assert.NotNull(ev.Geo);
            Assert.NotEqual(GREEN_BAY_IP, ev.Geo);

            var location = ev.GetLocation();
            Assert.Equal("US", location?.Country);
            Assert.Equal("WI", location?.Level1);
            Assert.Equal("Green Bay", location?.Locality);
        }

        [Fact]
        public async Task WillSetLocationFromRequestInfo() {
            var plugin = new GeoPlugin(await GetResolverAsync(Log), _options);
            var ev = new PersistentEvent();
            ev.AddRequestInfo(new RequestInfo { ClientIpAddress = GREEN_BAY_IP });
            await plugin.EventBatchProcessingAsync(new List<EventContext> { new EventContext(ev, OrganizationData.GenerateSampleOrganization(_billingManager, _plans), ProjectData.GenerateSampleProject()) });

            Assert.NotNull(ev.Geo);

            var location = ev.GetLocation();
            Assert.Equal("US", location?.Country);
            Assert.Equal("WI", location?.Level1);
            Assert.Equal("Green Bay", location?.Locality);
        }

        [Fact]
        public async Task WillSetLocationFromEnvironmentInfoInfo() {
            var plugin = new GeoPlugin(await GetResolverAsync(Log), _options);
            var ev = new PersistentEvent();
            ev.SetEnvironmentInfo(new EnvironmentInfo { IpAddress = $"127.0.0.1,{GREEN_BAY_IP}" });
            await plugin.EventBatchProcessingAsync(new List<EventContext> { new EventContext(ev, OrganizationData.GenerateSampleOrganization(_billingManager, _plans), ProjectData.GenerateSampleProject()) });

            Assert.NotNull(ev.Geo);

            var location = ev.GetLocation();
            Assert.Equal("US", location?.Country);
            Assert.Equal("WI", location?.Level1);
            Assert.Equal("Green Bay", location?.Locality);
        }

        [Fact]
        public async Task WillSetFromSingleGeo() {
            var plugin = new GeoPlugin(await GetResolverAsync(Log), _options);

            var contexts = new List<EventContext> {
                new EventContext(new PersistentEvent { Geo = GREEN_BAY_IP }, OrganizationData.GenerateSampleOrganization(_billingManager, _plans), ProjectData.GenerateSampleProject()),
                new EventContext(new PersistentEvent { Geo = GREEN_BAY_IP }, OrganizationData.GenerateSampleOrganization(_billingManager, _plans), ProjectData.GenerateSampleProject())
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
            var plugin = new GeoPlugin(await GetResolverAsync(Log), _options);

            var ev = new PersistentEvent();
            var greenBayEvent = new PersistentEvent { Geo = GREEN_BAY_IP };
            var irvingEvent = new PersistentEvent { Geo = IRVING_IP };
            await plugin.EventBatchProcessingAsync(new List<EventContext> {
                new EventContext(ev, OrganizationData.GenerateSampleOrganization(_billingManager, _plans), ProjectData.GenerateSampleProject()),
                new EventContext(greenBayEvent, OrganizationData.GenerateSampleOrganization(_billingManager, _plans), ProjectData.GenerateSampleProject()),
                new EventContext(irvingEvent, OrganizationData.GenerateSampleOrganization(_billingManager, _plans), ProjectData.GenerateSampleProject())
            });

#if DEBUG
            Assert.Equal(GREEN_BAY_COORDINATES, greenBayEvent.Geo);
#endif
            var location = greenBayEvent.GetLocation();
            Assert.Equal("US", location?.Country);
            Assert.Equal("WI", location?.Level1);
            Assert.Equal("Green Bay", location?.Locality);

#if DEBUG
            Assert.Equal(IRVING_COORDINATES, irvingEvent.Geo);
#endif
            location = irvingEvent.GetLocation();
            Assert.Equal("US", location?.Country);
            Assert.Equal("TX", location?.Level1);
            Assert.Equal("Irving", location?.Locality);
        }

        [Fact]
        public async Task ReverseGeocodeLookup() {
            var service = GetService<IGeocodeService>();
            if (service is NullGeocodeService)
                return;

            Assert.True(GeoResult.TryParse(GREEN_BAY_COORDINATES, out var coordinates));
            var location = await service.ReverseGeocodeAsync(coordinates.Latitude.GetValueOrDefault(), coordinates.Longitude.GetValueOrDefault());
            Assert.Equal("US", location?.Country);
            Assert.Equal("WI", location?.Level1);
            Assert.Equal("Brown County", location?.Level2);
            Assert.Equal("Green Bay", location?.Locality);
        }

        [Fact]
        public async Task WillSetMultipleFromEmptyGeo() {
            var plugin = new GeoPlugin(await GetResolverAsync(Log), _options);

            var ev = new PersistentEvent();
            var greenBayEvent = new PersistentEvent();
            greenBayEvent.SetEnvironmentInfo(new EnvironmentInfo { IpAddress = GREEN_BAY_IP });
            var irvingEvent = new PersistentEvent();
            irvingEvent.SetEnvironmentInfo(new EnvironmentInfo { IpAddress = IRVING_IP });
            await plugin.EventBatchProcessingAsync(new List<EventContext> {
                new EventContext(ev, OrganizationData.GenerateSampleOrganization(_billingManager, _plans), ProjectData.GenerateSampleProject()),
                new EventContext(greenBayEvent, OrganizationData.GenerateSampleOrganization(_billingManager, _plans), ProjectData.GenerateSampleProject()),
                new EventContext(irvingEvent, OrganizationData.GenerateSampleOrganization(_billingManager, _plans), ProjectData.GenerateSampleProject())
            });

#if DEBUG
            Assert.Equal(GREEN_BAY_COORDINATES, greenBayEvent.Geo);
#endif
            var location = greenBayEvent.GetLocation();
            Assert.Equal("US", location?.Country);
            Assert.Equal("WI", location?.Level1);
            Assert.Equal("Green Bay", location?.Locality);

#if DEBUG
            Assert.Equal(IRVING_COORDINATES, irvingEvent.Geo);
#endif
            location = irvingEvent.GetLocation();
            Assert.Equal("US", location?.Country);
            Assert.Equal("TX", location?.Level1);
            Assert.Equal("Irving", location?.Locality);
        }

        [Theory]
        [MemberData(nameof(IPData))]
        public async Task CanResolveIpAsync(string ip, bool canResolve) {
            var resolver = await GetResolverAsync(Log);
            var result = await resolver.ResolveIpAsync(ip);
            if (canResolve)
                Assert.NotNull(result);
            else
                Assert.Null(result);
        }

        [Fact]
        public async Task CanResolveIpFromCacheAsync() {
            var resolver = await GetResolverAsync(Log);

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
