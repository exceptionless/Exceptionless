using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.ServiceProviders;

namespace StackSnapshotJob {
    public class Program {
        public static int Main() {
            AppDomain.CurrentDomain.SetDataDirectory();

            var loggerFactory = Settings.Current.GetLoggerFactory();
            if (Settings.Current.DisableSnapshotJobs) {
                var logger = loggerFactory.CreateLogger(nameof(StackSnapshotJob));
                logger.Info("Snapshot Jobs are currently disabled.");
                return 0;
            }

            var serviceProvider = ServiceProvider.GetServiceProvider(Settings.JobBootstrappedServiceProvider, loggerFactory);
            var job = serviceProvider.GetService<Exceptionless.Core.Jobs.Elastic.StackSnapshotJob>();
            return new JobRunner(job, loggerFactory, runContinuous: false).RunInConsole();
        }
    }
}
