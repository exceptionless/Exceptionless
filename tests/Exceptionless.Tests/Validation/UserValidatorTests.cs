using Exceptionless.Core.Models;
using Exceptionless.Core.Validation;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Validation;

public sealed class UserValidatorTests : TestWithServices
{
    private readonly MiniValidationValidator _validator;

    public UserValidatorTests(ITestOutputHelper output) : base(output)
    {
        _validator = GetService<MiniValidationValidator>();
    }

    [Theory]
    [InlineData("Valid User", true)]
    [InlineData("", false)]
    public async Task ValidateFullNameAsync(string fullName, bool isValid)
    {
        var user = new User
        {
            FullName = fullName,
            EmailAddress = "valid@example.com",
            IsEmailAddressVerified = true
        };

        var result = await _validator.ValidateAsync(user);
        Assert.Equal(isValid, result.IsValid);
    }

    [Theory]
    [InlineData("valid@example.com", true)]
    [InlineData("invalid-email", false)]
    [InlineData("", false)]
    public async Task ValidateEmailAddressAsync(string email, bool isValid)
    {
        var user = new User
        {
            FullName = "Valid User",
            EmailAddress = email,
            IsEmailAddressVerified = true
        };

        var result = await _validator.ValidateAsync(user);
        Assert.Equal(isValid, result.IsValid);
    }

    [Theory]
    [InlineData(false, "token", true)]
    [InlineData(false, "", false)]
    [InlineData(false, null, false)]
    [InlineData(true, "token", false)]
    [InlineData(true, "", false)]
    [InlineData(true, null, true)]
    public async Task ValidateVerifyEmailAddressTokenAsync(bool isEmailAddressVerified, string? token, bool isValid)
    {
        var user = new User
        {
            FullName = "Valid User",
            EmailAddress = "valid@example.com",
            IsEmailAddressVerified = isEmailAddressVerified,
            VerifyEmailAddressToken = token,
            VerifyEmailAddressTokenExpiration = isEmailAddressVerified ? DateTime.MinValue : DateTime.UtcNow.AddDays(1)
        };

        var result = await _validator.ValidateAsync(user);
        Assert.Equal(isValid, result.IsValid);
    }

    [Theory]
    [InlineData(false, "token", null, false)]
    [InlineData(false, "token", "0001-01-01", false)]
    [InlineData(false, "token", "2024-10-01", true)]
    [InlineData(false, "token", "9999-12-31T23:59:59.9999999", true)]
    [InlineData(false, null, "2024-10-01", false)]
    [InlineData(true, "token", "2024-10-01", false)]
    [InlineData(true, "", "2024-10-01", false)]
    [InlineData(true, null, "2024-10-01", false)]
    public async Task ValidateVerifyEmailAddressTokenExpirationAsync(bool isEmailAddressVerified, string? token, string? expiration, bool isValid)
    {
        var tokenExpiration = expiration is null ? DateTime.MinValue : DateTime.Parse(expiration);

        var user = new User
        {
            FullName = "Valid User",
            EmailAddress = "valid@example.com",
            IsEmailAddressVerified = isEmailAddressVerified,
            VerifyEmailAddressToken = token,
            VerifyEmailAddressTokenExpiration = tokenExpiration
        };

        var result = await _validator.ValidateAsync(user);
        Assert.Equal(isValid, result.IsValid);
    }
}
