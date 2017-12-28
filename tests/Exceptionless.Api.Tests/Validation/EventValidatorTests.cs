using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Validation;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Validation {
    public class EventValidatorTests : TestBase {
        private readonly PersistentEvent _benchmarkEvent;
        private readonly PersistentEventValidator _validator;

        public EventValidatorTests(ITestOutputHelper output) : base(output) {
            _validator = new PersistentEventValidator();

            string path = Path.Combine("..", "..", "..", "Search", "Data", "event1.json");
            var parserPluginManager = GetService<EventParserPluginManager>();
            var events = parserPluginManager.ParseEvents(File.ReadAllText(path), 2, "exceptionless/2.0.0.0");
            _benchmarkEvent = events.First();
        }

        [Fact]
        public void RunBenchmark() {
            const int iterations = 10000;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) {
                var result = _validator.Validate(_benchmarkEvent);
                Assert.True(result.IsValid);
            }

            sw.Stop();
            _logger.LogInformation("Time: {Duration:g}, Avg: ({AverageTickDuration:g}ticks | {AverageDuration}ms)", sw.Elapsed, sw.ElapsedTicks / iterations, sw.ElapsedMilliseconds / iterations);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("1", true)]
        [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456", false)]
         public void ValidateTag(string tag, bool isValid) {
            var ev = new PersistentEvent { Type = Event.KnownTypes.Error, Date = SystemClock.OffsetNow, Id = "123456789012345678901234", OrganizationId = "123456789012345678901234", ProjectId = "123456789012345678901234", StackId = "123456789012345678901234" };
            ev.Tags.Add(tag);

            var result = _validator.Validate(ev);
            Assert.Equal(isValid, result.IsValid);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("1", true)]
        [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456", false)]
        public async Task ValidateTagAsync(string tag, bool isValid) {
            var ev = new PersistentEvent { Type = Event.KnownTypes.Error, Date = SystemClock.OffsetNow, Id = "123456789012345678901234", OrganizationId = "123456789012345678901234", ProjectId = "123456789012345678901234", StackId = "123456789012345678901234" };
            ev.Tags.Add(tag);

            var result = await _validator.ValidateAsync(ev);
            Assert.Equal(isValid, result.IsValid);
        }
        [Theory]
        [InlineData(null, true)]
        [InlineData("1234567", false)]
        [InlineData("12345678", true)]
        [InlineData("1234567890123456", true)]
        [InlineData("123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123", false)]
        public void ValidateReferenceId(string referenceId, bool isValid) {
            var result = _validator.Validate(new PersistentEvent { Type = Event.KnownTypes.Error, ReferenceId = referenceId, Date = SystemClock.OffsetNow, Id = "123456789012345678901234", OrganizationId = "123456789012345678901234", ProjectId = "123456789012345678901234", StackId = "123456789012345678901234" });
            Assert.Equal(isValid, result.IsValid);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData(-60d, true)]
        [InlineData(0d, true)]
        [InlineData(60d, true)]
        [InlineData(61d, false)]
        public void ValidateDate(double? minutes, bool isValid) {
            var date = minutes.HasValue ? SystemClock.OffsetNow.AddMinutes(minutes.Value) : DateTimeOffset.MinValue;
            var result = _validator.Validate(new PersistentEvent { Type = Event.KnownTypes.Error, Date = date, Id = "123456789012345678901234", OrganizationId = "123456789012345678901234", ProjectId = "123456789012345678901234", StackId = "123456789012345678901234" });
            Console.WriteLine(date + " " + result.IsValid + " " + String.Join(" ", result.Errors.Select(e => e.ErrorMessage)));
            Assert.Equal(isValid, result.IsValid);
        }

        [Theory]
        [InlineData(Event.KnownTypes.Error, true)]
        [InlineData(Event.KnownTypes.FeatureUsage, true)]
        [InlineData(Event.KnownTypes.Log, true)]
        [InlineData(Event.KnownTypes.NotFound, true)]
        [InlineData(Event.KnownTypes.SessionEnd, true)]
        [InlineData(Event.KnownTypes.Session, true)]
        [InlineData("12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901", false)]
        public void ValidateType(string type, bool isValid) {
            var result = _validator.Validate(new PersistentEvent { Type = type, Date = SystemClock.OffsetNow, Id = "123456789012345678901234", OrganizationId = "123456789012345678901234", ProjectId = "123456789012345678901234", StackId = "123456789012345678901234" });
            Assert.Equal(isValid, result.IsValid);
        }
    }
}