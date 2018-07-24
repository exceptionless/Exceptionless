using System;
using System.Threading.Tasks;
using Exceptionless;
using Exceptionless.Core;
using Exceptionless.Insulation.Jobs;
using Foundatio.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace EventPostsJob {
    public class Program {
        public static async Task<int> Main() {
            IServiceProvider serviceProvider = null;
            try {
                serviceProvider = JobServiceProvider.GetServiceProvider();
                var job = serviceProvider.GetService<Exceptionless.Core.Jobs.EventPostsJob>();
                return await new JobRunner(job, serviceProvider.GetRequiredService<ILoggerFactory>(), initialDelay: TimeSpan.FromSeconds(2), interval: TimeSpan.Zero, iterationLimit: Settings.Current.JobsIterationLimit).RunInConsoleAsync();
            } catch (Exception ex) {
                Log.Fatal(ex, "Job terminated unexpectedly");
                return 1;
            } finally {
                Log.CloseAndFlush();
                if (serviceProvider is IDisposable disposable) 
                    disposable.Dispose(); 
                await ExceptionlessClient.Default.ProcessQueueAsync();
            }
        }
    }
}