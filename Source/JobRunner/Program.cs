using System;
using System.Diagnostics;
using System.IO;
using CommandLine;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Utility;
using SimpleInjector;

namespace Exceptionless.JobRunner {
    internal class Program {
        private static int Main(string[] args) {
            try {
                var ca = new Options();
                
                if (!Parser.Default.ParseArguments(args, ca)) {
                    PauseIfDebug();
                    return 0;
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

                if (!ca.Quiet) {
                    OutputHeader();
                    Console.WriteLine("Starting {0}job type \"{1}\"...", ca.RunContinuously ? "continuous " : String.Empty, type.Name);
                }

                if (ca.RunContinuously)
                    job.RunContinuous(TimeSpan.FromMilliseconds(ca.Delay));
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

            return container;
        }

        private static void PauseIfDebug() {
            if (Debugger.IsAttached)
                Console.ReadKey();
        }
    }
}