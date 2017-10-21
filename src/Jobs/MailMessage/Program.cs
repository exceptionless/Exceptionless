using System;
using Exceptionless;
using Exceptionless.Core;
using Exceptionless.Insulation.Jobs;
using Foundatio.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace MailMessageJob {
    public class Program {
        public static int Main() {
            try {
                var serviceProvider = JobServiceProvider.GetServiceProvider();
                var job = serviceProvider.GetService<Exceptionless.Core.Jobs.MailMessageJob>();
                return new JobRunner(job, serviceProvider.GetRequiredService<ILoggerFactory>(), initialDelay: TimeSpan.FromSeconds(5), interval: TimeSpan.Zero, iterationLimit: Settings.Current.JobsIterationLimit).RunInConsole();
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
