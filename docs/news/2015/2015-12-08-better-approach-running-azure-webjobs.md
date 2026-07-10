---
title: "A Better Approach to Running Azure WebJobs"
---

# A Better Approach to Running Azure WebJobs

![Azure Webjobs](/assets/img/news/jobs-blog-header-image.jpg)

Lets talk about jobs in the Exceptionless world for a minute and **how you can use our methods to improve your Azure WebJobs.**

A job is a specific task/process that runs and does something like send a mail message, etc.

## Out with the Old

**Prior to version 3.1**, we used an early version of the Foundatio Jobs system to run our out-of-process jobs via Azure WebJobs. We found it to be quite a pain to figure out which jobs were running or eating up system resources because every job was titled Job.exe (just like figuring out the w3wp IIS process is running). Also, just to run an out-of-process job, one would have to compile the source, copy dependencies to a common bin folder, and then run an executable (Job.exe) with parameters that specify the job type.

These tedious and error-prone tasks that had to be completed just to get a job to run are a thing of the past.

## In with the New



**In Exceptionless 3.1** we focused on refining and improving jobs. To do so, we created a [new console application for each job](https://github.com/exceptionless/Exceptionless/blob/master/src/Exceptionless.Core/Jobs) and specified settings in the code versus [error prone command line options as shown here](https://github.com/exceptionless/Exceptionless/blob/master/src/Exceptionless.Core/Jobs/EventPostsJob.cs#L16-L22).

Now, with [Foundatio](/introducing-foundatio-3-0-async-efficiency/), our open source app building block solution used in Exceptionless, you just define a new [Job](https://github.com/FoundatioFx/Foundatio#jobs) that runs (via the run method) and you can use the [Foundatio Jobs API](https://github.com/FoundatioFx/Foundatio#jobs) to run the job in process, out of process, continuous, or one time without changing the implementation.

This new approach also gave us a great deployment strategy, for free. Simply copy the job executable and bin folders and run it anywhere!

### Jobs (processes) running in Azure as an Azure web job

![Exceptionless Jobs and Processes](/assets/img/news/Jobs-1024x670.jpg)

### How you can implement a better Azure WebJob

[Foundatio Jobs](https://github.com/FoundatioFx/Foundatio#jobs) allows you to run a long running process (in process or out of process) with out worrying about it being terminated prematurely. By using Foundatio Jobs you gain all of the following features **without changing your job implementation**:

* Run job in process
* Run job out of process
* Run job with a start up delay
* Run job in an continuous loop with an optional interval delay.

In this sample we'll just define a new class called HelloWorldJob that will hold our job that increments a counter and derives from JobBase. Please note that there are a few different base classes you can derive from based on your use case.

```cs
using Foundatio.Jobs;

public class HelloWorldJob : JobBase {
   public int RunCount { get; set; }

   protected override Task<JobResult> RunInternalAsync(JobRunContext context) {
       RunCount++;
       return Task.FromResult(JobResult.Success);
   }
}
```

Now that we have our job defined we can run our job in process with a few different options:

```cs
var job = new HelloWorldJob();
await job.RunAsync(); // job.RunCount = 1;
await job.RunContinuousAsync(iterationLimit: 2); // job.RunCount = 3;
await job.RunContinuousAsync(cancellationToken: new CancellationTokenSource(TimeSpan.FromMilliseconds(10)).Token); // job.RunCount > 10;
```

But our goal is to run this out of process in an Azure WebJob (this also works if you want to run this as a service or from the desktop).

The first step is to create a new console application and reference the [Foundatio NuGet Package](https://www.nuget.org/packages/Foundatio/) and the project that contains our HelloWorldJob. We are going to call our console application HelloWorldJob. Inside of the Program class, we'll update the main method to run our job.

```cs
using System;
using System.IO;
using JobSample;
using Foundatio.Jobs;
using Foundatio.ServiceProviders;

namespace HelloWorldJob {
    public class Program {
        public static int Main(string[] args) {
            // NOTE: This should be the path to your App_Data folder of your website.
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Api\App_Data");
            if (Directory.Exists(path))
                AppDomain.CurrentDomain.SetData("DataDirectory", path);

            // Get a service provider so we can create an instance of our job.
            var serviceProvider = ServiceProvider.GetServiceProvider("JobSample.JobBootstrappedServiceProvider,JobSample");

            var job = serviceProvider.GetService<JobSample.HelloWorldJob>();
            return new JobRunner(job, initialDelay: TimeSpan.FromSeconds(2), interval: TimeSpan.Zero).RunInConsole();
        }
    }
}
```

The last steps are to simply compile the project and deploy it to your Azure website!

### Questions?

If you have any questions please feel free to contact us via our contact page, in app message, [GitHub issues](https://github.com/FoundatioFx/Foundatio/issues) or [Discord](https://discord.gg/6HxgFCx).
