using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Foundatio.Jobs;

namespace CloseInactiveSessionsJob {
    public class Program {
        public static int Main(string[] args) {
            AppDomain.CurrentDomain.SetDataDirectory();

            return new JobRunner(Settings.Current.GetLoggerFactory()).RunInConsole(new JobRunOptions {
                JobType = typeof(Exceptionless.Core.Jobs.CloseInactiveSessionsJob),
                ServiceProviderTypeName = Settings.FoundatioBootstrapper,
                InstanceCount = 1,
                Interval = TimeSpan.FromMinutes(1),
                InitialDelay = TimeSpan.FromSeconds(30),
                RunContinuous = true
            });
        }
    }
}
