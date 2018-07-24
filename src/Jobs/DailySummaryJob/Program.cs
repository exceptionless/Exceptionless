using System;
using System.Threading.Tasks;
using Exceptionless;
using Exceptionless.Insulation.Jobs;
using Foundatio.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DailySummaryJob {
    public class Program {
        public static async Task<int> Main() {
            IServiceProvider serviceProvider = null;
            try {
                serviceProvider = JobServiceProvider.GetServiceProvider();
                var job = serviceProvider.GetService<Exceptionless.Core.Jobs.DailySummaryJob>();
                return await new JobRunner(job, serviceProvider.GetRequiredService<ILoggerFactory>(), initialDelay: TimeSpan.FromMinutes(1), interval: TimeSpan.FromHours(1)).RunInConsoleAsync();
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
