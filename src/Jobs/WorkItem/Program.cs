using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Insulation.Jobs;
using Foundatio.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace WorkItemJob {
    public class Program {
        public static int Main() {
            AppDomain.CurrentDomain.SetDataDirectory();

            var loggerFactory = Settings.Current.GetLoggerFactory();
            var serviceProvider = JobServiceProvider.CreateServiceProvider(loggerFactory);
            var job = serviceProvider.GetService<Foundatio.Jobs.WorkItemJob>();
            return new JobRunner(job, loggerFactory, initialDelay: TimeSpan.FromSeconds(2), interval: TimeSpan.Zero, iterationLimit: Settings.Current.JobsIterationLimit).RunInConsole();
        }
    }
}
