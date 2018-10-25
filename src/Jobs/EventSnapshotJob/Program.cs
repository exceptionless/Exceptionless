using System;
using System.Threading.Tasks;
using Exceptionless;
using Exceptionless.Core.Configuration;
using Exceptionless.Insulation.Jobs;
using Foundatio.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

namespace EventSnapshotJob {
    public class Program {
        public static async Task<int> Main() {
            IServiceProvider serviceProvider = null;
            try {
                serviceProvider = JobServiceProvider.GetServiceProvider();

                var elasticOptions = serviceProvider.GetRequiredService<IOptions<ElasticsearchOptions>>();
                if (!elasticOptions.Value.EnableSnapshotJobs) {
                    Log.Logger.Information("Snapshot Jobs are currently disabled.");
                    return 0;
                }

                var job = serviceProvider.GetService<Exceptionless.Core.Jobs.Elastic.EventSnapshotJob>();
                return await new JobRunner(job, serviceProvider.GetRequiredService<ILoggerFactory>(), runContinuous: false).RunInConsoleAsync();
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
