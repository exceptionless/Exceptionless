using System;
using System.Linq;
using Exceptionless.Core.Validation;
using Exceptionless.Core.Models;
using Xunit;
using Xunit.Extensions;

namespace Exceptionless.Api.Tests.Validation {
    public class EventValidatorTests {
        private readonly PersistentEventValidator _validator;

        public EventValidatorTests() {
            _validator = new PersistentEventValidator();
        }
        
        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("1", true)]
        [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456", false)]
         public void ValidateTag(string tag, bool isValid) {
             var ev = new PersistentEvent { Type = Event.KnownTypes.Error, Date = DateTimeOffset.Now, Id = "123456789012345678901234", OrganizationId = "123456789012345678901234", ProjectId = "123456789012345678901234", StackId = "123456789012345678901234" };
            ev.Tags.Add(tag);

            var result = _validator.Validate(ev);
            Assert.Equal(isValid, result.IsValid);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("1234567", false)]
        [InlineData("12345678", true)]
        [InlineData("1234567890123456", true)]
        [InlineData("123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123123456789012345678901234567890123", false)]
        public void ValidateReferenceId(string referenceId, bool isValid) {
            var result = _validator.Validate(new PersistentEvent { Type = Event.KnownTypes.Error, ReferenceId = referenceId, Date = DateTimeOffset.Now, Id = "123456789012345678901234", OrganizationId = "123456789012345678901234", ProjectId = "123456789012345678901234", StackId = "123456789012345678901234" });
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
        [InlineData(Event.KnownTypes.SessionStart, true)]
        [InlineData("12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901", false)]
        public void ValidateType(string type, bool isValid) {
            var result = _validator.Validate(new PersistentEvent { Type = type, Date = DateTimeOffset.Now, Id = "123456789012345678901234", OrganizationId = "123456789012345678901234", ProjectId = "123456789012345678901234", StackId = "123456789012345678901234" });
            Assert.Equal(isValid, result.IsValid);
        }
    }
}