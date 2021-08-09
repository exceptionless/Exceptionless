using System;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Extensions.Hosting.Implementation
{
    public class SelfDiagnosticsLoggingHostedService : IHostedService, IDisposable
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IDisposable forwarder;

        public SelfDiagnosticsLoggingHostedService(ILoggerFactory loggerFactory, EventLevel? minEventLevel = null)
        {
            this.loggerFactory = loggerFactory;

            // The sole purpose of this HostedService is to
            // start forwarding the self-diagnostics events
            // to the logger factory
            this.forwarder = new SelfDiagnosticsEventLogForwarder(this.loggerFactory, minEventLevel);
        }

        public void Dispose()
        {
            this.forwarder?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}