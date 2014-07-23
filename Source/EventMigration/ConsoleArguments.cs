using System;
using CodeSmith.Core.CommandLine;

namespace Exceptionless.EventMigration {
    public class ConsoleArguments {
        [Argument(ArgumentType.Required, ShortName = "s", LongName = "since", HelpText = "The last time that you ran the migration.")]
        public DateTime Since;
    }
}