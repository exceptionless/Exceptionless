using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Insulation.Jobs;
using Foundatio.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace RetentionLimitsJob {
    public class Program {
        public static int Main() {
            AppDomain.CurrentDomain.SetDataDirectory();

            var loggerFactory = Settings.GetLoggerFactory();
            var serviceProvider = JobServiceProvider.CreateServiceProvider(loggerFactory);
            var job = serviceProvider.GetService<Exceptionless.Core.Jobs.RetentionLimitsJob>();
            return new JobRunner(job, loggerFactory, initialDelay: TimeSpan.FromMinutes(15), interval: TimeSpan.FromHours(1)).RunInConsole();
        }
    }
}
