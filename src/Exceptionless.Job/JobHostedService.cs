using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.DateTimeExtensions;
using Foundatio.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Job {
    public class JobHostedService<T> : HostedService where T : IJob {
        protected readonly IServiceProvider _serviceProvider;
        protected readonly ILoggerFactory _loggerFactory;
        public JobHostedService(IServiceProvider serviceProvider, ILoggerFactory loggerFactory) {
            _serviceProvider = serviceProvider;
            _loggerFactory = loggerFactory;
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken) {
            var jobAttribute = typeof(T).GetCustomAttribute<JobAttribute>() ?? new JobAttribute();
            string jobName = jobAttribute.Name;

            if (String.IsNullOrEmpty(jobName)) {
                jobName = typeof(T).Name;
                if (jobName.EndsWith("Job"))
                    jobName = jobName.Substring(0, jobName.Length - 3);

                jobName = jobName.ToLower();
            }

            bool isContinuous = jobAttribute.IsContinuous;
            TimeSpan? interval = null;
            TimeSpan? delay = null;
            int limit = -1;

            if (!String.IsNullOrEmpty(jobAttribute.Interval))
                TimeUnit.TryParse(jobAttribute.Interval, out interval);

            if (!String.IsNullOrEmpty(jobAttribute.InitialDelay))
                TimeUnit.TryParse(jobAttribute.InitialDelay, out delay);

            if (jobAttribute.IterationLimit > 0)
                limit = jobAttribute.IterationLimit;

            var runner = new JobRunner(() => _serviceProvider.GetRequiredService<T>(), _loggerFactory, runContinuous: isContinuous, interval: interval, initialDelay: delay, iterationLimit: limit);
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
            return services.AddSingleton<IHostedService, JobHostedService<T>>();
        }
    }
}
