using Exceptionless.Core.Models;
using Exceptionless.Core.Validation;
using Xunit;

namespace Exceptionless.Tests.Validation;

public sealed class WebHookValidatorTests : TestWithServices
{
    private const string ValidObjectId = "123456789012345678901234";
    private readonly MiniValidationValidator _validator;

    public WebHookValidatorTests(ITestOutputHelper output) : base(output)
    {
        _validator = GetService<MiniValidationValidator>();
    }

    private WebHook CreateValidWebHook()
    {
        return new WebHook
        {
            Id = ValidObjectId,
            OrganizationId = ValidObjectId,
            ProjectId = ValidObjectId,
            Url = "https://localhost.com/webhook",
            EventTypes = [WebHook.KnownEventTypes.NewError],
            Version = WebHook.KnownVersions.Version2
        };
    }

    [Fact]
    public async Task Validate_WhenOrganizationIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.OrganizationId = ValidObjectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenOrganizationIdIsInvalid_ReturnsError(string? organizationId)
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.OrganizationId = organizationId!;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(WebHook.OrganizationId)));
    }

    [Fact]
    public async Task Validate_WhenProjectIdIsEmptyAndOrganizationIdIsEmpty_ReturnsError()
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.OrganizationId = "";
        webHook.ProjectId = "";

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(WebHook.ProjectId)));
    }

    [Fact]
    public async Task Validate_WhenProjectIdIsValidButOrganizationIdIsEmpty_ReturnsError()
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.OrganizationId = "";
        webHook.ProjectId = ValidObjectId;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("http://localhost:8080/hook")]
    public async Task Validate_WhenUrlIsValid_ReturnsSuccess(string url)
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.Url = url;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenUrlIsEmpty_ReturnsError(string? url)
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.Url = url!;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(WebHook.Url)));
    }

    [Fact]
    public async Task Validate_WhenEventTypesIsEmpty_ReturnsError()
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.EventTypes = [];

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(WebHook.EventTypes)));
    }

    [Fact]
    public async Task Validate_WhenEventTypesIsNull_ReturnsError()
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.EventTypes = null!;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(WebHook.EventTypes)));
    }

    [Theory]
    [InlineData(WebHook.KnownEventTypes.NewError)]
    [InlineData(WebHook.KnownEventTypes.CriticalError)]
    [InlineData(WebHook.KnownEventTypes.NewEvent)]
    [InlineData(WebHook.KnownEventTypes.CriticalEvent)]
    [InlineData(WebHook.KnownEventTypes.StackRegression)]
    [InlineData(WebHook.KnownEventTypes.StackPromoted)]
    public async Task Validate_WhenEventTypeIsKnown_ReturnsSuccess(string eventType)
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.EventTypes = [eventType];

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("unknown_event_type")]
    [InlineData("invalid")]
    public async Task Validate_WhenEventTypeIsUnknown_ReturnsError(string eventType)
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.EventTypes = [eventType];

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => k.StartsWith("EventTypes"));
    }

    [Fact]
    public async Task Validate_WhenMultipleKnownEventTypes_ReturnsSuccess()
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.EventTypes =
        [
            WebHook.KnownEventTypes.NewError,
            WebHook.KnownEventTypes.CriticalError,
            WebHook.KnownEventTypes.StackRegression
        ];

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task Validate_WhenMultipleEventTypesWithOneInvalid_ReturnsError()
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.EventTypes =
        [
            WebHook.KnownEventTypes.NewError,
            "invalid_type"
        ];

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task Validate_WhenVersionIsValid_ReturnsSuccess(string version)
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.Version = version;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_WhenVersionIsEmpty_ReturnsError(string? version)
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.Version = version!;

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors.Keys, k => String.Equals(k, nameof(WebHook.Version)));
    }

    [Fact]
    public async Task Validate_WhenWebHookIsValid_ReturnsSuccess()
    {
        // Arrange
        var webHook = CreateValidWebHook();

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenWebHookIsValid_ReturnsSuccess()
    {
        // Arrange
        var webHook = CreateValidWebHook();

        // Act
        var (isValid, errors) = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.True(isValid);
    }
}
