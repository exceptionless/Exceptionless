using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Foundatio.Utility;
using Foundatio.Jobs;
using Foundatio.ServiceProviders;

namespace CloseInactiveSessionsJob {
    public class Program {
        public static int Main() {
            AppDomain.CurrentDomain.SetDataDirectory();

            var loggerFactory = Settings.Current.GetLoggerFactory();
            var serviceProvider = ServiceProvider.GetServiceProvider(Settings.JobBootstrappedServiceProvider, loggerFactory);
            var job = serviceProvider.GetService<Exceptionless.Core.Jobs.CloseInactiveSessionsJob>();
            return new JobRunner(job, loggerFactory, initialDelay: TimeSpan.FromSeconds(30), interval: TimeSpan.FromSeconds(30)).RunInConsole();
        }
    }
}
