using System;
using System.Diagnostics;
using System.IO;
using CodeSmith.Core.CommandLine;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Utility;
using SimpleInjector;

namespace Exceptionless.JobRunner {
    internal class Program {
        private static int Main(string[] args) {
            OutputHeader();
            
            try {
                var ca = new ConsoleArguments();
                if (Parser.ParseHelp(args)) {
                    OutputUsageHelp();
                    PauseIfDebug();
                    return 0;
                }

                if (!Parser.ParseArguments(args, ca, Console.Error.WriteLine)) {
                    OutputUsageHelp();
                    PauseIfDebug();
                    return 1;
                }

                Console.WriteLine();

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

                if (ca.RunContinuously)
                    job.RunContinuous(ca.Delay);
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

        public static Container CreateContainer() {
            var container = new Container();
            container.Options.AllowOverridingRegistrations = true;
            container.Options.PropertySelectionBehavior = new InjectAttributePropertySelectionBehavior();

            container.RegisterPackage<Core.Bootstrapper>();

            return container;
        }

        private static void PauseIfDebug() {
            if (Debugger.IsAttached)
                Console.ReadKey();
        }

        private static void OutputHeader() {
            Console.WriteLine("Exceptionless Job Runner v{0}", ThisAssembly.AssemblyInformationalVersion);
            Console.WriteLine("Copyright (c) 2012-{0} Exceptionless.  All rights reserved.", DateTime.Now.Year);
            Console.WriteLine();
        }

        private static void OutputUsageHelp() {
            Console.WriteLine("     - Exceptionless Job Runner -");
            Console.WriteLine();
            Console.WriteLine(Parser.ArgumentsUsage(typeof(ConsoleArguments)));
            Console.WriteLine("Usage samples:");
            Console.WriteLine();
            Console.WriteLine("  job /t:\"Exceptionless.Core.Jobs.ProcessEventsJob, Exceptionless.Core\"");
        }
    }
}