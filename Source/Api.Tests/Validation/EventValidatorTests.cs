using System;
using System.Linq;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Validation;
using Exceptionless.Models;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Api.Tests.Validation {
    public class EventValidatorTests {
        private readonly EventValidator _validator;

        public EventValidatorTests() {
            _validator = new EventValidator();
        }
        
        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("1", true)]
        [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456", false)]
         public void ValidateTag(string tag, bool isValid) {
            var ev = new Event { Type = Event.KnownTypes.Error, Date = DateTimeOffset.Now };
            ev.Tags.Add(tag);

            var result = _validator.Validate(ev);
            Assert.Equal(isValid, result.IsValid);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("1234567", false)]
        [InlineData("12345678", true)]
        [InlineData("1234567890123456", true)]
        [InlineData("123456789012345678901234567890123", false)]
        public void ValidateReferenceId(string referenceId, bool isValid) {
            var result = _validator.Validate(new Event { Type = Event.KnownTypes.Error, ReferenceId = referenceId, Date = DateTimeOffset.Now });
            Assert.Equal(isValid, result.IsValid);
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData(-60d, true)]
        [InlineData(0d, true)]
        [InlineData(60d, true)]
        [InlineData(61d, false)]
        public void ValidateDate(double? minutes, bool isValid) {
            var date = minutes.HasValue ? DateTimeOffset.Now.AddMinutes(minutes.Value) : DateTimeOffset.MinValue;
            var result = _validator.Validate(new Event { Type = Event.KnownTypes.Error, Date = date });
            Console.WriteLine(date + " " + result.IsValid);
            Assert.Equal(isValid, result.IsValid);
        }

        [Theory]
        [InlineData(Event.KnownTypes.Error, true)]
        [InlineData(Event.KnownTypes.FeatureUsage, true)]
        [InlineData(Event.KnownTypes.Log, true)]
        [InlineData(Event.KnownTypes.NotFound, true)]
        [InlineData(Event.KnownTypes.SessionEnd, true)]
        [InlineData(Event.KnownTypes.SessionStart, true)]
        [InlineData("12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901", false)]
        public void ValidateType(string type, bool isValid) {
            var result = _validator.Validate(new Event { Type = type, Date = DateTimeOffset.Now });
            Assert.Equal(isValid, result.IsValid);
        }
    }
}