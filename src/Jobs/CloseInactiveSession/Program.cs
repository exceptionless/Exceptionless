using System;
using Exceptionless;
using Exceptionless.Insulation.Jobs;
using Foundatio.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CloseInactiveSessionsJob {
    public class Program {
        public static int Main() {
            try {
                var serviceProvider = JobServiceProvider.GetServiceProvider();
                var job = serviceProvider.GetService<Exceptionless.Core.Jobs.CloseInactiveSessionsJob>();
                return new JobRunner(job, serviceProvider.GetRequiredService<ILoggerFactory>(), initialDelay: TimeSpan.FromSeconds(30), interval: TimeSpan.FromSeconds(30)).RunInConsole();
            } catch (Exception ex) {
                Log.Fatal(ex, "Job terminated unexpectedly");
                return 1;
            } finally {
                Log.CloseAndFlush();
                ExceptionlessClient.Default.ProcessQueue();
            }
        }
    }
}
