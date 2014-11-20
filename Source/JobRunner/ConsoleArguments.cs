using System;
using CodeSmith.Core.CommandLine;

namespace Exceptionless.JobRunner {
    public class ConsoleArguments {
        [Argument(ArgumentType.Required, ShortName = "t", LongName = "jobtype", HelpText = "The type of job that you wish to run.")]
        public string JobType;

        [Argument(ArgumentType.AtMostOnce, ShortName = "c", LongName = "continuous", HelpText = "Run the job in a continuous loop.")]
        public bool RunContinuously;

        [Argument(ArgumentType.AtMostOnce, ShortName = "d", LongName = "delay", HelpText = "Amount of time in milliseconds to delay between continuous job runs.")]
        public int Delay = 0;
    }
}