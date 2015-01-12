using System;
using CommandLine;
using CommandLine.Text;

namespace Exceptionless.JobRunner {
    public class Options {
        [Option('t', "jobtype", Required = true, HelpText = "The type of job that you wish to run.")]
        public string JobType { get; set; }

        [Option('c', "continuous", Required = false, DefaultValue = false, HelpText = "Run the job in a continuous loop.")]
        public bool RunContinuously { get; set; }

        [Option('q', "quiet", Required = false, DefaultValue = false, HelpText = "Don't output header text.")]
        public bool Quiet { get; set; }

        [Option('d', "delay", Required = false, DefaultValue = 0, HelpText = "Amount of time in milliseconds to delay between continuous job runs.")]
        public int Delay { get; set; }

        [HelpOption]
        public string GetUsage() {
            var help = new HelpText {
                Heading = new HeadingInfo("Exceptionless Job Runner", ThisAssembly.AssemblyInformationalVersion),
                Copyright = new CopyrightInfo("Exceptionless", DateTime.Today.Year),
                AdditionalNewLineAfterOption = true,
                AddDashesToOption = true
            };

            help.AddPreOptionsLine("Usage: job -t \"Exceptionless.Core.Jobs.ProcessEventsJob, Exceptionless.Core\"");
            help.AddOptions(this);

            return help;
        }
    }
}