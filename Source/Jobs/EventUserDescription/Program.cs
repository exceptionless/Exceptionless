using System;
using System.IO;
using Exceptionless.Core;
using Foundatio.Jobs;
using Foundatio.Logging;

namespace EventUserDescriptionsJob {
    public class Program {
        public static int Main(string[] args) {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\Api\App_Data");
            if (Directory.Exists(path))
                AppDomain.CurrentDomain.SetData("DataDirectory", path);

            Logger.SetMinimumLogLevel(Settings.Current.MinimumLogLevel);

            return JobRunner.RunInConsole(new JobRunOptions {
                JobType = typeof(Exceptionless.Core.Jobs.EventUserDescriptionsJob),
                ServiceProviderTypeName = "Exceptionless.Insulation.Jobs.FoundatioBootstrapper,Exceptionless.Insulation",
                InstanceCount = 1,
                Interval = TimeSpan.Zero,
                InitialDelay = TimeSpan.FromSeconds(3),
                RunContinuous = true
            });
        }
    }
}
