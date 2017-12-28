using System;
using System.Threading.Tasks;
using Exceptionless;
using Exceptionless.Insulation.Jobs;
using Foundatio.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace DownloadGeoIPDatabaseJob {
    public class Program {
        public static async Task<int> Main() {
            try {
                var serviceProvider = JobServiceProvider.GetServiceProvider();
                var job = serviceProvider.GetService<Exceptionless.Core.Jobs.DownloadGeoIPDatabaseJob>();
                return await new JobRunner(job, serviceProvider.GetRequiredService<ILoggerFactory>(), runContinuous: false).RunInConsoleAsync();
            } catch (Exception ex) {
                Log.Fatal(ex, "Job terminated unexpectedly");
                return 1;
            } finally {
                Log.CloseAndFlush();
                await ExceptionlessClient.Default.ProcessQueueAsync();
            }
        }
    }
}
