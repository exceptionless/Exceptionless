using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Foundatio.Jobs;

namespace RetentionLimitsJob {
    public class Program {
        public static int Main(string[] args) {
            AppDomain.CurrentDomain.SetDataDirectory();

            return new JobRunner(Settings.Current.GetLoggerFactory()).RunInConsole(new JobRunOptions {
                JobType = typeof(Exceptionless.Core.Jobs.RetentionLimitsJob),
                ServiceProviderTypeName = Settings.FoundatioBootstrapper,
                InstanceCount = 1,
                Interval = TimeSpan.FromDays(1),
                InitialDelay = TimeSpan.FromMinutes(15),
                RunContinuous = true
            });
        }
    }
}
