using System;
using System.Threading.Tasks;
using Exceptionless;
using Exceptionless.Insulation.Jobs;
using Foundatio.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace StackEventCountJob {
    public class Program {
        public static async Task<int> Main() {
            IServiceProvider serviceProvider = null;
            try {
                serviceProvider = JobServiceProvider.GetServiceProvider();
                return await new JobRunner(serviceProvider.GetService<Exceptionless.Core.Jobs.StackEventCountJob>(), serviceProvider.GetRequiredService<ILoggerFactory>(), initialDelay: TimeSpan.FromSeconds(2), interval: TimeSpan.FromSeconds(5)).RunInConsoleAsync();
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
