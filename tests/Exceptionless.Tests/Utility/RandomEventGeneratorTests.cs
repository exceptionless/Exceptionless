using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Utility;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Utility;

public class RandomEventGeneratorTests : TestWithServices
{
    private readonly ITextSerializer _serializer;

    public RandomEventGeneratorTests(ITestOutputHelper output) : base(output)
    {
        _serializer = GetService<ITextSerializer>();
    }

    [Fact]
    public void Generate_WithRegularEvents_IncludesErrorLevelLogEvent()
    {
        // Arrange
        var generator = new Exceptionless.Core.Utility.RandomEventGenerator(System.TimeProvider.System);

        // Act
        var events = generator.Generate("organization", "project", 10);

        // Assert
        Assert.Contains(events, generatedEvent =>
            generatedEvent.Type == Event.KnownTypes.Log &&
            generatedEvent.Data is not null &&
            generatedEvent.Data.TryGetValue(Event.KnownDataKeys.Level, out object? level) &&
            String.Equals(level as string, "Error", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_WithErrorEvents_ReusesStackSignatures()
    {
        // Arrange
        var generator = new Exceptionless.Core.Utility.RandomEventGenerator(System.TimeProvider.System);

        // Act
        var signatures = generator.Generate("organization", "project", 200)
            .Select(GetErrorSignature)
            .Where(signature => !String.IsNullOrEmpty(signature))
            .ToList();

        // Assert
        Assert.True(signatures.Count > 8);
        Assert.True(signatures.Distinct().Count() < signatures.Count);
    }

    [Fact]
    public void Generate_WithSampleEvents_IncludesReferenceIds()
    {
        // Arrange
        var generator = new Exceptionless.Core.Utility.RandomEventGenerator(TimeProvider.System);

        // Act
        var events = generator.Generate("organization", "project", 10);

        // Assert
        Assert.Contains(events, generatedEvent =>
            generatedEvent.ReferenceId is { Length: 10 } referenceId &&
            referenceId.All(Char.IsLetterOrDigit));
    }

    private string? GetErrorSignature(Event ev)
    {
        if (ev.Data is null)
            return null;

        if (ev.Data.TryGetValue(Event.KnownDataKeys.Error, out object? errorValue) && errorValue is Error error)
            return new ErrorSignature(error, _serializer).SignatureHash;

        if (ev.Data.TryGetValue(Event.KnownDataKeys.SimpleError, out object? simpleErrorValue) && simpleErrorValue is SimpleError simpleError)
            return $"{simpleError.Type}:{simpleError.StackTrace}";

        return null;
    }
}
