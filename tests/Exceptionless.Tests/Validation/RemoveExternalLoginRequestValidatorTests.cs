using Exceptionless.Core.Validation;
using Exceptionless.Web.Models;
using Xunit;

namespace Exceptionless.Tests.Validation;

public sealed class RemoveExternalLoginRequestValidatorTests(ITestOutputHelper output) : TestWithServices(output)
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("subject", true)]
    public async Task ValidateAsync_ProviderUserId_RequiresNonWhitespaceValue(string? providerUserId, bool expectedIsValid)
    {
        // Arrange
        var request = new RemoveExternalLoginRequest
        {
            ProviderName = "github",
            ProviderUserId = new ValueFromBody<string>(providerUserId!)
        };

        // Act
        var result = await GetService<MiniValidationValidator>().ValidateAsync(request);

        // Assert
        Assert.Equal(expectedIsValid, result.IsValid);
        if (!expectedIsValid)
            Assert.Contains(nameof(RemoveExternalLoginRequest.ProviderUserId), result.Errors.Keys);
    }
}
