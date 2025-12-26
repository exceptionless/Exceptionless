using System.Diagnostics;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Validation;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Validation;

public sealed class EventValidatorTests : TestWithServices
{
    private const string ValidObjectId = "123456789012345678901234";
    private readonly PersistentEvent _benchmarkEvent;
    private readonly PersistentEventValidator _validator;

    public EventValidatorTests(ITestOutputHelper output) : base(output)
    {
        _validator = new PersistentEventValidator(TimeProvider);

        string path = Path.Combine("..", "..", "..", "Search", "Data", "event1.json");
        var parserPluginManager = GetService<EventParserPluginManager>();
        var events = parserPluginManager.ParseEvents(File.ReadAllText(path), 2, "exceptionless/2.0.0.0");
        _benchmarkEvent = events[0];
    }

    private PersistentEvent CreateValidEvent()
    {
        return new PersistentEvent
        {
            Id = ValidObjectId,
            OrganizationId = ValidObjectId,
            ProjectId = ValidObjectId,
            StackId = ValidObjectId,
            Type = Event.KnownTypes.Error,
            Date = DateTimeOffset.Now
        };
    }


    [Fact]
    public void Validate_WhenRunningBenchmark_CompletesInReasonableTime()
    {
        // Arrange
        const int iterations = 10000;
        var sw = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var result = _validator.Validate(_benchmarkEvent);
            Assert.True(result.IsValid);
        }

        sw.Stop();

        // Assert
        _logger.LogInformation("Time: {Duration:g}, Avg: ({AverageTickDuration:g}ticks | {AverageDuration}ms)", sw.Elapsed, sw.ElapsedTicks / iterations, sw.ElapsedMilliseconds / iterations);
    }

    [Fact]
    public void Validate_WhenIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Id = ValidObjectId;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenIdIsInvalid_ReturnsError(string? id)
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Id = id!;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(PersistentEvent.Id)));
    }

    [Fact]
    public void Validate_WhenOrganizationIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.OrganizationId = ValidObjectId;

        // Act
        var result = _validator.Validate(ev);

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
        var ev = CreateValidEvent();
        ev.OrganizationId = organizationId!;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(PersistentEvent.OrganizationId)));
    }

    [Fact]
    public void Validate_WhenProjectIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.ProjectId = ValidObjectId;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenProjectIdIsInvalid_ReturnsError(string? projectId)
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.ProjectId = projectId!;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(PersistentEvent.ProjectId)));
    }

    [Fact]
    public void Validate_WhenStackIdIsValidObjectId_ReturnsSuccess()
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.StackId = ValidObjectId;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("1234567890123456789012345")]
    [InlineData("invalid-id")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenStackIdIsInvalid_ReturnsError(string? stackId)
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.StackId = stackId!;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(PersistentEvent.StackId)));
    }

    [Fact]
    public void Validate_WhenTagIsValid_ReturnsSuccess()
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Tags ??= new TagSet();
        ev.Tags.Add("1");

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Validate_WhenTagIsEmpty_ReturnsError(string? tag)
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Tags ??= new TagSet();
        ev.Tags.Add(tag);

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WhenTagExceedsMaxLength_ReturnsError()
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Tags ??= new TagSet();
        ev.Tags.Add(new string('a', 256));

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ValidateAsync_WhenTagIsEmpty_ReturnsError(string? tag)
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Tags ??= new TagSet();
        ev.Tags.Add(tag);

        // Act
        var result = await _validator.ValidateAsync(ev);

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1234567890123456")]
    public void Validate_WhenReferenceIdIsValid_ReturnsSuccess(string? referenceId)
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.ReferenceId = referenceId;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenReferenceIdIsTooShort_ReturnsError()
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.ReferenceId = "1234567";

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WhenReferenceIdIsTooLong_ReturnsError()
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.ReferenceId = new string('1', 321);

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(-60d)]
    [InlineData(0d)]
    [InlineData(60d)]
    public void Validate_WhenDateIsWithinRange_ReturnsSuccess(double minutes)
    {
        // Arrange
        var date = DateTimeOffset.Now.AddMinutes(minutes);
        var ev = CreateValidEvent();
        ev.Date = date;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        _logger.LogInformation(date + " " + result.IsValid + " " + String.Join(" ", result.Errors.Select(e => e.ErrorMessage)));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenDateIsMinValue_ReturnsError()
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Date = DateTimeOffset.MinValue;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WhenDateIsTooFarInFuture_ReturnsError()
    {
        // Arrange
        var date = DateTimeOffset.Now.AddMinutes(61);
        var ev = CreateValidEvent();
        ev.Date = date;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        _logger.LogInformation(date + " " + result.IsValid + " " + String.Join(" ", result.Errors.Select(e => e.ErrorMessage)));
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(Event.KnownTypes.Error)]
    [InlineData(Event.KnownTypes.FeatureUsage)]
    [InlineData(Event.KnownTypes.Log)]
    [InlineData(Event.KnownTypes.NotFound)]
    [InlineData(Event.KnownTypes.SessionEnd)]
    [InlineData(Event.KnownTypes.Session)]
    public void Validate_WhenTypeIsValid_ReturnsSuccess(string type)
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Type = type;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenTypeExceedsMaxLength_ReturnsError()
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Type = new string('x', 101);

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Valid message")]
    public void Validate_WhenMessageIsValid_ReturnsSuccess(string? message)
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Message = message;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenMessageIsEmpty_ReturnsError()
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Message = String.Empty;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(PersistentEvent.Message)));
    }

    [Fact]
    public void Validate_WhenMessageExceedsMaxLength_ReturnsError()
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Message = new string('x', 2001);

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(PersistentEvent.Message)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Valid source")]
    public void Validate_WhenSourceIsValid_ReturnsSuccess(string? source)
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Source = source;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenSourceIsEmpty_ReturnsError()
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Source = String.Empty;

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(PersistentEvent.Source)));
    }

    [Fact]
    public void Validate_WhenSourceExceedsMaxLength_ReturnsError()
    {
        // Arrange
        var ev = CreateValidEvent();
        ev.Source = new string('x', 2001);

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => String.Equals(e.PropertyName, nameof(PersistentEvent.Source)));
    }

    [Fact]
    public void Validate_WhenEventIsValid_ReturnsSuccess()
    {
        // Arrange
        var ev = CreateValidEvent();

        // Act
        var result = _validator.Validate(ev);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_WhenEventIsValid_ReturnsSuccess()
    {
        // Arrange
        var ev = CreateValidEvent();

        // Act
        var result = await _validator.ValidateAsync(ev);

        // Assert
        Assert.True(result.IsValid);
    }
}
