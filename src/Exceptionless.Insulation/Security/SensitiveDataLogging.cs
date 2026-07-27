using Exceptionless.Core;
using Exceptionless.Core.Configuration;
using Serilog;

namespace Exceptionless.Insulation.Security;

/// <summary>
/// Prevents configuration option objects from being destructured into logs.
/// Option objects contain credentials and API keys, so they must be treated as scalars.
/// </summary>
public static class SensitiveDataLogging
{
    public static LoggerConfiguration ApplySensitiveDataLogging(this LoggerConfiguration configuration)
    {
        return configuration
            .Destructure.AsScalar<AppOptions>()
            .Destructure.AsScalar<AuthOptions>()
            .Destructure.AsScalar<CacheOptions>()
            .Destructure.AsScalar<ElasticsearchOptions>()
            .Destructure.AsScalar<EmailOptions>()
            .Destructure.AsScalar<IntercomOptions>()
            .Destructure.AsScalar<MessageBusOptions>()
            .Destructure.AsScalar<QueueOptions>()
            .Destructure.AsScalar<StorageOptions>()
            .Destructure.AsScalar<StripeOptions>()
            .Destructure.AsScalar<SlackOptions>()
            .Destructure.AsScalar<OAuthServerOptions>()
            .Destructure.AsScalar<SourceMapOptions>();
    }
}
