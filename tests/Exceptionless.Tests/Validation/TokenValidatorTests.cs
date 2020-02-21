using Exceptionless.Core.Validation;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Validation {
    public sealed class TokenValidatorTests : TestWithServices {
        private readonly TokenValidator _validator;

        public TokenValidatorTests(ServicesFixture fixture, ITestOutputHelper output) : base(fixture, output) {
            _validator = new TokenValidator();
        }

        [Theory]
        [InlineData(TokenType.Access, false, true)]
        [InlineData(TokenType.Access, true, true)]
        [InlineData(TokenType.Authentication, false, true)]
        [InlineData(TokenType.Authentication, true, false)]
         public void VerifyIsDisabled(TokenType type, bool isDisabled, bool isValid) {
             var token = new Token {
                 Id = SampleDataService.TEST_API_KEY,
                 OrganizationId = SampleDataService.TEST_ORG_ID,
                 Type = type,
                 IsDisabled = isDisabled,
                 CreatedUtc = SystemClock.UtcNow,
                 UpdatedUtc = SystemClock.UtcNow
             };
             
            var result = _validator.Validate(token);
            if (!result.IsValid)
                _logger.LogInformation(result.ToString());
            
            Assert.Equal(isValid, result.IsValid);
        }
    }
}