﻿using System;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Foundatio.Extensions;
using Foundatio.Jobs;
using Foundatio.ServiceProviders;

namespace EventPostsJob {
    public class Program {
        public static int Main() {
            AppDomain.CurrentDomain.SetDataDirectory();

            var loggerFactory = Settings.Current.GetLoggerFactory();
            var serviceProvider = ServiceProvider.GetServiceProvider(Settings.JobBootstrappedServiceProvider, loggerFactory);
            var job = serviceProvider.GetService<Exceptionless.Core.Jobs.EventPostsJob>();
            return new JobRunner(job, loggerFactory, initialDelay: TimeSpan.FromSeconds(2), interval: TimeSpan.Zero, instanceCount: 2).RunInConsole();
        }
    }
}
