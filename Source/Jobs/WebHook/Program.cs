using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Foundatio.Jobs;

namespace WebHooksJob {
    public class Program {
        public static int Main(string[] args) {
            AppDomain.CurrentDomain.SetDataDirectory();

            return new JobRunner(Settings.Current.GetLoggerFactory()).RunInConsole(new JobRunOptions {
                JobType = typeof(Exceptionless.Core.Jobs.WebHooksJob),
                ServiceProviderTypeName = Settings.FoundatioBootstrapper,
                InstanceCount = 1,
                Interval = TimeSpan.Zero,
                InitialDelay = TimeSpan.FromSeconds(5),
                RunContinuous = true
            });
        }
    }
}
