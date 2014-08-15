using System;
using CodeSmith.Core.CommandLine;

namespace Exceptionless.JobRunner {
    public class ConsoleArguments {
        [Argument(ArgumentType.Required, ShortName = "t", LongName = "jobtype", HelpText = "The type of job that you wish to run.")]
        public string JobType;
    }
}