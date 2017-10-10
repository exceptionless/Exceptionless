using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Insulation.Jobs;
using Foundatio.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace EventNotificationsJob {
    public class Program {
        public static int Main() {
            AppDomain.CurrentDomain.SetDataDirectory();

            var loggerFactory = Settings.GetLoggerFactory();
            var serviceProvider = JobServiceProvider.CreateServiceProvider(loggerFactory);
            var job = serviceProvider.GetService<Exceptionless.Core.Jobs.EventNotificationsJob>();
            return new JobRunner(job, loggerFactory, initialDelay: TimeSpan.FromSeconds(5), interval: TimeSpan.Zero, iterationLimit: Settings.Current.JobsIterationLimit).RunInConsole();
        }
    }
}
