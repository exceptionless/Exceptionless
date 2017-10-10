using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Insulation.Jobs;
using Foundatio.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CleanupSnapshotJob {
    public class Program {
        public static int Main() {
            AppDomain.CurrentDomain.SetDataDirectory();

            var loggerFactory = Settings.GetLoggerFactory();
            var serviceProvider = JobServiceProvider.CreateServiceProvider(loggerFactory);

            if (Settings.Current.DisableSnapshotJobs) {
                var logger = loggerFactory.CreateLogger(nameof(CleanupSnapshotJob));
                logger.LogInformation("Snapshot Jobs are currently disabled.");
                return 0;
            }

            var job = serviceProvider.GetService<Exceptionless.Core.Jobs.Elastic.CleanupSnapshotJob>();
            return new JobRunner(job, loggerFactory, runContinuous: false).RunInConsole();
        }
    }
}