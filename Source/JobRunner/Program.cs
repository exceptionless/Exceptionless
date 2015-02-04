using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CommandLine;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Utility;
using Exceptionless.EventMigration;
using SimpleInjector;
using StackExchange.Redis;
using Log = NLog;

namespace Exceptionless.JobRunner {
    internal class Program {
        private static int Main(string[] args) {
            try {
                var ca = new Options();
                if (!Parser.Default.ParseArguments(args, ca)) {
                    PauseIfDebug();
                    return 0;
                }

                var type = Type.GetType(ca.JobType);
                if (type == null) {
                    Console.Error.WriteLine("Unable to resolve type: \"{0}\".", ca.JobType);
                    PauseIfDebug();
                    return 1;
                }

                var container = CreateContainer();
                var job = container.GetInstance(Type.GetType(ca.JobType)) as JobBase;
                if (job == null) {
                    Console.Error.WriteLine("Job Type must derive from Job.");
                    PauseIfDebug();
                    return 1;
                }

                Log.GlobalDiagnosticsContext.Set("job", type.Name);
                if (!ca.Quiet) {
                    OutputHeader();
                    Console.WriteLine("Starting {0}job type \"{1}\" on machine \"{2}\"...", ca.RunContinuously ? "continuous " : String.Empty, type.Name, Environment.MachineName);
                }
                
                WatchForShutdown();
                if (ca.RunContinuously)
                    job.RunContinuous(TimeSpan.FromMilliseconds(ca.Delay), token: _cancellationTokenSource.Token);
                else
                    job.Run();

                PauseIfDebug();
            } catch (FileNotFoundException e) {
                Console.Error.WriteLine("{0} ({1})", e.Message, e.FileName);
                PauseIfDebug();
                return 1;
            } catch (Exception e) {
                Console.Error.WriteLine(e.ToString());
                PauseIfDebug();
                return 1;
            }

            return 0;
        }

        private static void OutputHeader() {
            Console.WriteLine("Exceptionless Job Runner v{0}", ThisAssembly.AssemblyInformationalVersion);
            Console.WriteLine("Copyright (c) 2012-{0} Exceptionless.  All rights reserved.", DateTime.Now.Year);
            Console.WriteLine();
        }

        public static Container CreateContainer() {
            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.PropertySelectionBehavior = new InjectAttributePropertySelectionBehavior();

            container.RegisterPackage<Bootstrapper>();

            if (Settings.Current.EnableRedis)
                container.RegisterSingle<IQueue<EventMigrationBatch>>(() => new RedisQueue<EventMigrationBatch>(container.GetInstance<ConnectionMultiplexer>()));
            else
                container.RegisterSingle<IQueue<EventMigrationBatch>>(() => new InMemoryQueue<EventMigrationBatch>());

            return container;
        }

        private static void PauseIfDebug() {
            if (Debugger.IsAttached)
                Console.ReadKey();
        }

        private static string _webJobsShutdownFile;
        private static readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private static void WatchForShutdown() {
            ShutdownEventCatcher.Shutdown += args => {
                _cancellationTokenSource.Cancel();
                Console.WriteLine("Job shutdown event signaled: {0}", args.Reason);
            };

            _webJobsShutdownFile = Environment.GetEnvironmentVariable("WEBJOBS_SHUTDOWN_FILE");
            if (String.IsNullOrEmpty(_webJobsShutdownFile))
                return;

            var watcher = new FileSystemWatcher(Path.GetDirectoryName(_webJobsShutdownFile));
            watcher.Created += OnFileChanged;
            watcher.Changed += OnFileChanged;
            watcher.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.LastWrite;
            watcher.IncludeSubdirectories = false;
            watcher.EnableRaisingEvents = true;
        }

        private static void OnFileChanged(object sender, FileSystemEventArgs e) {
            if (e.FullPath.IndexOf(Path.GetFileName(_webJobsShutdownFile), StringComparison.OrdinalIgnoreCase) >= 0) {
                _cancellationTokenSource.Cancel();
                Console.WriteLine("Job shutdown signaled.");
            }
        }
    }
}