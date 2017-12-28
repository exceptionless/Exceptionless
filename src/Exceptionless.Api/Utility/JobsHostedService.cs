using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Api.Utility {
    public class JobsHostedService : IHostedService {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerFactory _loggerFactory;

        public JobsHostedService(IServiceProvider serviceProvider, ILoggerFactory loggerFactory) {
            _serviceProvider = serviceProvider;
            _loggerFactory = loggerFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken) {
            Core.Bootstrapper.RunJobs(_serviceProvider, _loggerFactory, cancellationToken);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) {
            return Task.CompletedTask;
        }
    }
}
