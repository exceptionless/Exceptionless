using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Foundatio.Jobs;
using Foundatio.ServiceProviders;

namespace RetentionLimitsJob {
    public class Program {
        public static int Main() {
            AppDomain.CurrentDomain.SetDataDirectory();

            var loggerFactory = Settings.Current.GetLoggerFactory();
            var serviceProvider = ServiceProvider.GetServiceProvider(Settings.JobBootstrappedServiceProvider, loggerFactory);
            var job = serviceProvider.GetService<Exceptionless.Core.Jobs.RetentionLimitsJob>();
            return new JobRunner(job, loggerFactory, initialDelay: TimeSpan.FromMinutes(15), interval: TimeSpan.FromHours(1)).RunInConsole();
        }
    }
}
