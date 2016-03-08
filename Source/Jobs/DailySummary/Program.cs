using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Foundatio.Jobs;

namespace DailySummaryJob {
    public class Program {
        public static int Main(string[] args) {
            AppDomain.CurrentDomain.SetDataDirectory();

            return new JobRunner(Settings.Current.GetLoggerFactory()).RunInConsole(new JobRunOptions {
                JobType = typeof(Exceptionless.Core.Jobs.DailySummaryJob),
                ServiceProviderTypeName = Settings.FoundatioBootstrapper,
                InstanceCount = 1,
                Interval = TimeSpan.FromHours(1),
                InitialDelay = TimeSpan.FromMinutes(1),
                RunContinuous = true
            });
        }
    }
}
