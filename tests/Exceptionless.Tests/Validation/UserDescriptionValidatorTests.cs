using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Validation;
using Xunit;

namespace Exceptionless.Tests.Validation;

public sealed class UserDescriptionValidatorTests : TestWithServices
{
    private readonly MiniValidationValidator _validator;

    public UserDescriptionValidatorTests(ITestOutputHelper output) : base(output)
    {
        _validator = GetService<MiniValidationValidator>();
    }

    private UserDescription CreateValidUserDescription()
    {
        return new UserDescription
        {
            EmailAddress = "test@localhost.com",
            Description = "This is a test description"
        };
    }

    [Theory]
    [InlineData("valid@localhost.com")]
    [InlineData("test.user@localhost.co.uk")]
    [InlineData("user+tag@localhost.com")]
    public async Task Validate_WhenEmailAddressIsValid_ReturnsSuccess(string email)
    {
        // Arrange
        var userDescription = CreateValidUserDescription();
        userDescription.EmailAddress = email;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(userDescription);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@nodomain.com")]
    public async Task Validate_WhenEmailAddressIsInvalid_ReturnsError(string email)
    {
        // Arrange
        var userDescription = CreateValidUserDescription();
        userDescription.EmailAddress = email;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(userDescription);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(UserDescription.EmailAddress)));
    }

    [Fact]
    public async Task Validate_WhenEmailAddressIsNull_ReturnsSuccess()
    {
        // Arrange
        var userDescription = CreateValidUserDescription();
        userDescription.EmailAddress = null;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(userDescription);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task Validate_WhenEmailAddressIsEmpty_ReturnsFailure()
    {
        // Arrange
        var userDescription = CreateValidUserDescription();
        userDescription.EmailAddress = "";

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(userDescription);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(UserDescription.EmailAddress)));
    }

    [Theory]
    [InlineData("Valid description")]
    [InlineData("a")]
    public async Task Validate_WhenDescriptionIsValid_ReturnsSuccess(string description)
    {
        // Arrange
        var userDescription = CreateValidUserDescription();
        userDescription.Description = description;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(userDescription);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenDescriptionIsEmpty_ReturnsError(string? description)
    {
        // Arrange
        var userDescription = CreateValidUserDescription();
        userDescription.Description = description;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(userDescription);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(UserDescription.Description)));
    }

    [Fact]
    public async Task Validate_WhenUserDescriptionIsValid_ReturnsSuccess()
    {
        // Arrange
        var userDescription = CreateValidUserDescription();

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(userDescription);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task Validate_WhenOnlyDescriptionIsSet_ReturnsSuccess()
    {
        // Arrange
        var userDescription = new UserDescription
        {
            Description = "Just a description, no email"
        };

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(userDescription);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenUserDescriptionIsValid_ReturnsSuccess()
    {
        // Arrange
        var userDescription = CreateValidUserDescription();

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(userDescription);

        // Assert
        Assert.True(isValid);
    }
}
