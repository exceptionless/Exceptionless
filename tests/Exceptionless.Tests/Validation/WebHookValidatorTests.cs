using Exceptionless.Core.Models;
using Exceptionless.Core.Validation;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Validation;

public sealed class WebHookValidatorTests : TestWithServices
{
    private const string ValidObjectId = "123456789012345678901234";
    private readonly WebHookValidator _validator;

    public WebHookValidatorTests(ITestOutputHelper output) : base(output)
    {
        _validator = new WebHookValidator();
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
    public void Validate_WhenOrganizationIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.OrganizationId = ValidObjectId;

        // Act
        var result = _validator.Validate(webHook);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenOrganizationIdIsInvalid_ReturnsError(string? organizationId)
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.OrganizationId = organizationId!;

        // Act
        var result = _validator.Validate(webHook);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(WebHook.OrganizationId)));
    }

    [Fact]
    public void Validate_WhenProjectIdIsEmptyAndOrganizationIdIsEmpty_ReturnsError()
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.OrganizationId = "";
        webHook.ProjectId = "";

        // Act
        var result = _validator.Validate(webHook);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(WebHook.ProjectId)));
    }

    [Fact]
    public void Validate_WhenProjectIdIsValidButOrganizationIdIsEmpty_ReturnsError()
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.OrganizationId = "";
        webHook.ProjectId = ValidObjectId;

        // Act
        var result = _validator.Validate(webHook);

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("http://localhost:8080/hook")]
    public void Validate_WhenUrlIsValid_ReturnsSuccess(string url)
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.Url = url;

        // Act
        var result = _validator.Validate(webHook);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenUrlIsEmpty_ReturnsError(string? url)
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.Url = url!;

        // Act
        var result = _validator.Validate(webHook);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(WebHook.Url)));
    }

    [Fact]
    public void Validate_WhenEventTypesIsEmpty_ReturnsError()
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.EventTypes = [];

        // Act
        var result = _validator.Validate(webHook);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(WebHook.EventTypes)));
    }

    [Fact]
    public void Validate_WhenEventTypesIsNull_ReturnsError()
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.EventTypes = null!;

        // Act
        var result = _validator.Validate(webHook);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(WebHook.EventTypes)));
    }

    [Theory]
    [InlineData(WebHook.KnownEventTypes.NewError)]
    [InlineData(WebHook.KnownEventTypes.CriticalError)]
    [InlineData(WebHook.KnownEventTypes.NewEvent)]
    [InlineData(WebHook.KnownEventTypes.CriticalEvent)]
    [InlineData(WebHook.KnownEventTypes.StackRegression)]
    [InlineData(WebHook.KnownEventTypes.StackPromoted)]
    public void Validate_WhenEventTypeIsKnown_ReturnsSuccess(string eventType)
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.EventTypes = [eventType];

        // Act
        var result = _validator.Validate(webHook);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("unknown_event_type")]
    [InlineData("invalid")]
    public void Validate_WhenEventTypeIsUnknown_ReturnsError(string eventType)
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.EventTypes = [eventType];

        // Act
        var result = _validator.Validate(webHook);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName.StartsWith("EventTypes"));
    }

    [Fact]
    public void Validate_WhenMultipleKnownEventTypes_ReturnsSuccess()
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
        var result = _validator.Validate(webHook);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenMultipleEventTypesWithOneInvalid_ReturnsError()
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.EventTypes =
        [
            WebHook.KnownEventTypes.NewError,
            "invalid_type"
        ];

        // Act
        var result = _validator.Validate(webHook);

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public void Validate_WhenVersionIsValid_ReturnsSuccess(string version)
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.Version = version;

        // Act
        var result = _validator.Validate(webHook);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenVersionIsEmpty_ReturnsError(string? version)
    {
        // Arrange
        var webHook = CreateValidWebHook();
        webHook.Version = version!;

        // Act
        var result = _validator.Validate(webHook);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(WebHook.Version)));
    }

    [Fact]
    public void Validate_WhenWebHookIsValid_ReturnsSuccess()
    {
        // Arrange
        var webHook = CreateValidWebHook();

        // Act
        var result = _validator.Validate(webHook);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenWebHookIsValid_ReturnsSuccess()
    {
        // Arrange
        var webHook = CreateValidWebHook();

        // Act
        var result = await _validator.ValidateAsync(webHook);

        // Assert
        Assert.True(result.IsValid);
    }
}
