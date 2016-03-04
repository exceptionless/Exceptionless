using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Foundatio.Jobs;

namespace WorkItemJob {
    public class Program {
        public static int Main(string[] args) {
            AppDomain.CurrentDomain.SetDataDirectory();

            return new JobRunner(Settings.Current.GetLoggerFactory()).RunInConsole(new JobRunOptions {
                JobType = typeof(Foundatio.Jobs.WorkItemJob),
                ServiceProviderTypeName = Settings.FoundatioBootstrapper,
                InstanceCount = 2,
                Interval = TimeSpan.Zero,
                InitialDelay = TimeSpan.FromSeconds(2),
                RunContinuous = true
            });
        }
    }
}
