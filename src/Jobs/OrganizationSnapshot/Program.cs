using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Foundatio.Extensions;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.ServiceProviders;

namespace OrganizationSnapshotJob {
    public class Program {
        public static int Main() {
            AppDomain.CurrentDomain.SetDataDirectory();
            
            var loggerFactory = Settings.Current.GetLoggerFactory();
            if (Settings.Current.DisableSnapshotJobs) {
                var logger = loggerFactory.CreateLogger(nameof(OrganizationSnapshotJob));
                logger.Info("Snapshot Jobs are currently disabled.");
                return 0;
            }

            var serviceProvider = ServiceProvider.GetServiceProvider(Settings.JobBootstrappedServiceProvider, loggerFactory);
            var job = serviceProvider.GetService<Exceptionless.Core.Jobs.Elastic.OrganizationSnapshotJob>();
            return new JobRunner(job, loggerFactory, runContinuous: false).RunInConsole();
        }
    }
}
