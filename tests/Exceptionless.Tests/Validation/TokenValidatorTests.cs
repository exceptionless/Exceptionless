using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Validation;
using Xunit;

namespace Exceptionless.Tests.Validation;

public sealed class TokenValidatorTests : TestWithServices
{
    private const string ValidObjectId = "123456789012345678901234";
    private readonly MiniValidationValidator _validator;

    public TokenValidatorTests(ITestOutputHelper output) : base(output)
    {
        _validator = GetService<MiniValidationValidator>();
    }

    private Token CreateValidToken()
    {
        return new Token
        {
            Id = SampleDataService.TEST_API_KEY,
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Type = TokenType.Access,
            IsDisabled = false,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    [Theory]
    [InlineData("test-api-key")]
    public async Task Validate_WhenIdIsValid_ReturnsSuccess(string id)
    {
        // Arrange
        var token = CreateValidToken();
        token.Id = id;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenIdIsEmpty_ReturnsError(string? id)
    {
        // Arrange
        var token = CreateValidToken();
        token.Id = id!;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Token.Id)));
    }

    [Fact]
    public async Task Validate_WhenOrganizationIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var token = CreateValidToken();
        token.OrganizationId = ValidObjectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    public async Task Validate_WhenOrganizationIdIsInvalidObjectId_ReturnsError(string organizationId)
    {
        // Arrange
        var token = CreateValidToken();
        token.OrganizationId = organizationId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Token.OrganizationId)));
    }

    [Fact]
    public async Task Validate_WhenOrganizationIdIsEmptyAndProjectIdIsSet_ReturnsError()
    {
        // Arrange
        var token = CreateValidToken();
        token.OrganizationId = "";
        token.ProjectId = ValidObjectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Token.OrganizationId)));
    }

    [Fact]
    public void Validate_WhenOrganizationIdIsEmptyAndUserIdIsEmpty_ReturnsError()
    {
        // Arrange
        var token = CreateValidToken();
        token.OrganizationId = "";
        token.UserId = "";

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(Token.OrganizationId)));
    }

    [Fact]
    public void Validate_WhenOrganizationIdIsEmptyButUserIdIsSet_ReturnsSuccess()
    {
        // Arrange
        var token = CreateValidToken();
        token.OrganizationId = "";
        token.UserId = ValidObjectId;
        token.ProjectId = "";

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_WhenProjectIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var token = CreateValidToken();
        token.ProjectId = ValidObjectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    public async Task Validate_WhenProjectIdIsInvalidObjectId_ReturnsError(string projectId)
    {
        // Arrange
        var token = CreateValidToken();
        token.ProjectId = projectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Token.ProjectId)));
    }

    [Fact]
    public void Validate_WhenProjectIdIsEmpty_ReturnsSuccess()
    {
        // Arrange
        var token = CreateValidToken();
        token.ProjectId = "";

        // Act
        var result = _validator.Validate(token);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_WhenDefaultProjectIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var token = CreateValidToken();
        token.DefaultProjectId = ValidObjectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    public async Task Validate_WhenDefaultProjectIdIsInvalidObjectId_ReturnsError(string defaultProjectId)
    {
        // Arrange
        var token = CreateValidToken();
        token.DefaultProjectId = defaultProjectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Token.DefaultProjectId)));
    }

    [Fact]
    public async Task Validate_WhenDefaultProjectIdIsSetAndProjectIdIsDefined_ReturnsError()
    {
        // Arrange
        var token = CreateValidToken();
        token.ProjectId = ValidObjectId;
        token.DefaultProjectId = ValidObjectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Token.DefaultProjectId)));
    }

    [Fact]
    public async Task Validate_WhenUserIdIsSetAndProjectIdIsDefined_ReturnsError()
    {
        // Arrange
        var token = CreateValidToken();
        token.ProjectId = ValidObjectId;
        token.UserId = ValidObjectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Token.UserId)));
    }

    [Fact]
    public async Task Validate_WhenCreatedUtcIsDefault_ReturnsError()
    {
        // Arrange
        var token = CreateValidToken();
        token.CreatedUtc = default;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Token.CreatedUtc)));
    }

    [Fact]
    public async Task Validate_WhenUpdatedUtcIsDefault_ReturnsError()
    {
        // Arrange
        var token = CreateValidToken();
        token.UpdatedUtc = default;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(Token.UpdatedUtc)));
    }

    [Theory]
    [InlineData(TokenType.Access, false)]
    [InlineData(TokenType.Access, true)]
    [InlineData(TokenType.Authentication, false)]
    public async Task Validate_WhenTokenTypeAndDisabledStateAreValid_ReturnsSuccess(TokenType type, bool isDisabled)
    {
        // Arrange
        var token = new Token
        {
            Id = SampleDataService.TEST_API_KEY,
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Type = type,
            IsDisabled = isDisabled,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        if (!isValid)
            _logger.LogInformation(String.Join(", ", errors.SelectMany(e => e.Value)));

        Assert.True(isValid);
    }

    [Fact]
    public async Task Validate_WhenAuthenticationTokenIsDisabled_ReturnsError()
    {
        // Arrange
        var token = new Token
        {
            Id = SampleDataService.TEST_API_KEY,
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Type = TokenType.Authentication,
            IsDisabled = true,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task Validate_WhenTokenIsValid_ReturnsSuccess()
    {
        // Arrange
        var token = CreateValidToken();

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenTokenIsValid_ReturnsSuccess()
    {
        // Arrange
        var token = CreateValidToken();

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(token);

        // Assert
        Assert.True(isValid);
    }
}
