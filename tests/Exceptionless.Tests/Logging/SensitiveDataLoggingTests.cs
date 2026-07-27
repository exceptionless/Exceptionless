using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Exceptionless.Insulation.Security;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Exceptionless.Tests.Logging;

public class SensitiveDataLoggingTests
{
    [Fact]
    public void ApplySensitiveDataLogging_DoesNotDestructureConfigurationObjects()
    {
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .ApplySensitiveDataLogging()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("Loaded configuration {@Options}", new AppOptions());

        var rendered = sink.Events.Single().Properties["Options"].ToString();
        Assert.DoesNotContain("InternalProjectId", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("ExceptionlessApiKey", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplySensitiveDataLogging_DoesNotDestructureNestedSensitiveOptions()
    {
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration()
            .ApplySensitiveDataLogging()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("Loaded configuration {@Options}", new EmailOptions());

        var rendered = sink.Events.Single().Properties["Options"].ToString();
        Assert.DoesNotContain("SmtpPassword", rendered, StringComparison.Ordinal);
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
