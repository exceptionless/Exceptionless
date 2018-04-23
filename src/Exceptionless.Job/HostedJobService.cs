using System;
using System.Threading;
using System.Threading.Tasks;
using Foundatio.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Job {
    public class HostedJobService<T> : HostedService where T : IJob {
        protected readonly IServiceProvider _serviceProvider;
        protected readonly ILoggerFactory _loggerFactory;

        public HostedJobService(IServiceProvider serviceProvider, ILoggerFactory loggerFactory) {
            _serviceProvider = serviceProvider;
            _loggerFactory = loggerFactory;
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken) {
            var jobOptions = JobOptions.GetDefaults<T>(() => _serviceProvider.GetRequiredService<T>());
            var runner = new JobRunner(jobOptions, _loggerFactory);
            return runner.RunAsync();
        }
    }

    public class HostedJobService : HostedService {
        protected readonly Func<IJob> _jobFactory;
        protected readonly ILoggerFactory _loggerFactory;

        public HostedJobService(Func<IJob> jobFactory, ILoggerFactory loggerFactory) {
            _jobFactory = jobFactory;
            _loggerFactory = loggerFactory;
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken) {
            var job = _jobFactory();
            var jobOptions = JobOptions.GetDefaults(job.GetType(), job);
            var runner = new JobRunner(jobOptions, _loggerFactory);
            return runner.RunAsync();
        }
    }


    public abstract class HostedService : IHostedService {
        private Task _executingTask;
        private CancellationTokenSource _cts;

        public Task StartAsync(CancellationToken cancellationToken) {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = ExecuteAsync(_cts.Token);
            return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken) {
            if (_executingTask == null)
                return;

            _cts.Cancel();
            await Task.WhenAny(_executingTask, Task.Delay(-1, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
        }

        protected abstract Task ExecuteAsync(CancellationToken cancellationToken);
    }

    public static class JobExtensions {
        public static IServiceCollection AddJob<T>(this IServiceCollection services) where T: IJob {
            return services.AddSingleton<IHostedService, HostedJobService<T>>();
        }

        public static IServiceCollection AddJob(this IServiceCollection services, Func<IServiceProvider, IJob> jobFactory) {
            return services.AddSingleton<IHostedService>(sp => new HostedJobService(() => jobFactory(sp), sp.GetRequiredService<ILoggerFactory>()));
        }
    }
}
