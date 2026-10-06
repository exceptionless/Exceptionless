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
        var generator = new Exceptionless.Core.Utility.RandomEventGenerator(System.TimeProvider.System);

        // Act
        var events = generator.Generate("organization", "project", 10);

        // Assert
        Assert.Contains(events, generatedEvent =>
            generatedEvent.ReferenceId is { Length: 10 } referenceId &&
            referenceId.All(Char.IsLetterOrDigit));
    }

    [Fact]
    public void GenerateError_WithStructuredError_CreatesRichNestedStackTrace()
    {
        // Arrange
        var generator = new Exceptionless.Core.Utility.RandomEventGenerator(System.TimeProvider.System);

        // Act
        var error = generator.GenerateError();

        // Assert
        Assert.NotNull(error.Inner);
        Assert.NotNull(error.StackTrace);
        Assert.InRange(error.StackTrace.Count, 18, 32);
        Assert.Contains(error.StackTrace, frame =>
            frame.GenericArguments is { Count: > 0 } &&
            frame.Parameters is { Count: > 1 } &&
            frame.Parameters.Any(parameter =>
                !String.IsNullOrEmpty(parameter.TypeNamespace) &&
                parameter.GenericArguments is { Count: > 0 }));
        Assert.Contains(error.StackTrace, frame =>
            !String.IsNullOrEmpty(frame.FileName) &&
            frame.LineNumber.HasValue &&
            frame.Column.HasValue);
        Assert.Contains(error.StackTrace, frame => frame.DeclaringType?.Contains('+') == true);
        Assert.Contains(error.StackTrace, frame => frame.ModuleId.HasValue);
        Assert.Contains(error.StackTrace, frame => frame.Data?.ContainsKey("ILOffset") == true);
        Assert.Contains(error.StackTrace, frame => frame.Data?.ContainsKey("NativeOffset") == true);
    }

    [Fact]
    public void GenerateSimpleError_WithSimpleError_CreatesRealisticNestedStackTrace()
    {
        // Arrange
        var generator = new Exceptionless.Core.Utility.RandomEventGenerator(System.TimeProvider.System);

        // Act
        var error = generator.GenerateSimpleError();

        // Assert
        Assert.NotNull(error.Inner);
        string stackTrace = Assert.IsType<string>(error.StackTrace);
        Assert.Contains("EventPipeline.RunAsync", stackTrace);
        Assert.Contains("CancellationToken cancellationToken", stackTrace);
        Assert.Contains("EventPipeline.cs:line 84", stackTrace);
        Assert.True(stackTrace.Split('\n').Length >= 7);
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
