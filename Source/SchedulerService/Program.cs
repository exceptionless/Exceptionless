using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using System.ServiceProcess;
using CodeSmith.Core.Scheduler;
using Exceptionless;
using Exceptionless.Core.Utility;
using NLog;
using SimpleInjector;

namespace SchedulerService {
    public static class Program {
        public static void Main() {
            System.Diagnostics.Trace.Listeners.Add(new NLogTraceListener());
            if (ExceptionlessClient.Default.Configuration.Enabled) {
               // ExceptionlessClient.Default.Log = new NLogExceptionlessLog();
               // ExceptionlessClient.Default.Startup();
            }

            var container = new Container();

            // try and load modules from all assemblies referenced in the job configuration
            container.RegisterPackages(GetSchedulerAssemblies());
            JobManager.Current.SetDependencyResolver(new SimpleInjectorCoreDependencyResolver(container));

            ServiceBase.Run(new ServiceBase[] { new Scheduler() });
        }

        public static IEnumerable<Assembly> GetSchedulerAssemblies() {
            // gather all of the assemblies referenced in the job config
            var assemblies = new HashSet<Assembly>();
            var jobManager = ConfigurationManager.GetSection("jobManager") as JobManagerSection;
            if (jobManager == null)
                throw new ConfigurationErrorsException("Could not find 'jobManager' section in app.config.");

            foreach (ProviderSettings providerSettings in jobManager.JobLockProviders) {
                Type jobLockType = Type.GetType(providerSettings.Type, false, true);
                if (jobLockType == null)
                    throw new ApplicationException(String.Format("Unable to load type \"{0}\" referenced in the app.config. Make sure the specified assembly is in the same folder as the service.", providerSettings.Type));
                assemblies.Add(jobLockType.Assembly);
            }

            var jobs = new List<IJobConfiguration>();
            jobs.AddRange(jobManager.Jobs);

            foreach (JobProvider provider in jobManager.JobProviders)
                jobs.AddRange(provider.GetJobs());

            foreach (var job in jobs) {
                Type jobType = Type.GetType(job.Type, false, true);
                if (jobType == null)
                    throw new ApplicationException(String.Format("Unable to load type \"{0}\" referenced in the app.config. Make sure the specified assembly is in the same folder as the service.", job.Type));
                assemblies.Add(jobType.Assembly);

                if (String.IsNullOrEmpty(job.JobHistoryProvider))
                    continue;

                Type historyType = Type.GetType(job.JobHistoryProvider, false, true);
                if (historyType == null)
                    throw new ApplicationException(String.Format("Unable to load type \"{0}\" referenced in the app.config. Make sure the specified assembly is in the same folder as the service.", job.JobHistoryProvider));
                assemblies.Add(historyType.Assembly);
            }

            return assemblies;
        }
    }
}
