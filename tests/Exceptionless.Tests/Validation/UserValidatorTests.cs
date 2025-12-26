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

    [Fact]
    public async Task ValidateAsync_WhenFullNameIsValid_ReturnsSuccess()
    {
        // Arrange
        var user = new User
        {
            FullName = "Valid User",
            EmailAddress = "valid@localhost.com",
            IsEmailAddressVerified = true
        };

        // Act
        var result = await _validator.ValidateAsync(user);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenFullNameIsEmpty_ReturnsError()
    {
        // Arrange
        var user = new User
        {
            FullName = "",
            EmailAddress = "valid@localhost.com",
            IsEmailAddressVerified = true
        };

        // Act
        var result = await _validator.ValidateAsync(user);

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("")]
    public async Task ValidateAsync_WhenEmailAddressIsInvalid_ReturnsError(string email)
    {
        // Arrange
        var user = new User
        {
            FullName = "Valid User",
            EmailAddress = email,
            IsEmailAddressVerified = true
        };

        // Act
        var result = await _validator.ValidateAsync(user);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenNotVerifiedAndTokenIsSet_ReturnsSuccess()
    {
        // Arrange
        var user = new User
        {
            FullName = "Valid User",
            EmailAddress = "valid@localhost.com",
            IsEmailAddressVerified = false,
            VerifyEmailAddressToken = "token",
            VerifyEmailAddressTokenExpiration = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var result = await _validator.ValidateAsync(user);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task ValidateAsync_WhenNotVerifiedAndTokenIsEmpty_ReturnsError(string? token)
    {
        // Arrange
        var user = new User
        {
            FullName = "Valid User",
            EmailAddress = "valid@localhost.com",
            IsEmailAddressVerified = false,
            VerifyEmailAddressToken = token,
            VerifyEmailAddressTokenExpiration = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var result = await _validator.ValidateAsync(user);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenVerifiedAndTokenIsSet_ReturnsError()
    {
        // Arrange
        var user = new User
        {
            FullName = "Valid User",
            EmailAddress = "valid@localhost.com",
            IsEmailAddressVerified = true,
            VerifyEmailAddressToken = "token",
            VerifyEmailAddressTokenExpiration = DateTime.MinValue
        };

        // Act
        var result = await _validator.ValidateAsync(user);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenVerifiedAndTokenIsNull_ReturnsSuccess()
    {
        // Arrange
        var user = new User
        {
            FullName = "Valid User",
            EmailAddress = "valid@localhost.com",
            IsEmailAddressVerified = true,
            VerifyEmailAddressToken = null,
            VerifyEmailAddressTokenExpiration = DateTime.MinValue
        };

        // Act
        var result = await _validator.ValidateAsync(user);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenNotVerifiedAndTokenExpirationIsValid_ReturnsSuccess()
    {
        // Arrange
        var user = new User
        {
            FullName = "Valid User",
            EmailAddress = "valid@localhost.com",
            IsEmailAddressVerified = false,
            VerifyEmailAddressToken = "token",
            VerifyEmailAddressTokenExpiration = DateTime.Parse("2024-10-01")
        };

        // Act
        var result = await _validator.ValidateAsync(user);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0001-01-01")]
    public async Task ValidateAsync_WhenNotVerifiedAndTokenExpirationIsInvalid_ReturnsError(string? expiration)
    {
        // Arrange
        var tokenExpiration = expiration is null ? DateTime.MinValue : DateTime.Parse(expiration);
        var user = new User
        {
            FullName = "Valid User",
            EmailAddress = "valid@localhost.com",
            IsEmailAddressVerified = false,
            VerifyEmailAddressToken = "token",
            VerifyEmailAddressTokenExpiration = tokenExpiration
        };

        // Act
        var result = await _validator.ValidateAsync(user);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenNotVerifiedAndTokenIsNullWithExpiration_ReturnsError()
    {
        // Arrange
        var user = new User
        {
            FullName = "Valid User",
            EmailAddress = "valid@localhost.com",
            IsEmailAddressVerified = false,
            VerifyEmailAddressToken = null,
            VerifyEmailAddressTokenExpiration = DateTime.Parse("2024-10-01")
        };

        // Act
        var result = await _validator.ValidateAsync(user);

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("token")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ValidateAsync_WhenVerifiedWithTokenAndExpiration_ReturnsError(string? token)
    {
        // Arrange
        var user = new User
        {
            FullName = "Valid User",
            EmailAddress = "valid@localhost.com",
            IsEmailAddressVerified = true,
            VerifyEmailAddressToken = token,
            VerifyEmailAddressTokenExpiration = DateTime.Parse("2024-10-01")
        };

        // Act
        var result = await _validator.ValidateAsync(user);

        // Assert
        Assert.False(result.IsValid);
    }
}
