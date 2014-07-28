using System;
using CodeSmith.Core.CommandLine;

namespace Exceptionless.EventMigration {
    public class ConsoleArguments {
        [Argument(ArgumentType.AtMostOnce, ShortName = "r", LongName = "resume", HelpText = "Should resume from the most recent items in the index.")]
        public bool Resume;

        [Argument(ArgumentType.AtMostOnce, ShortName = "s", LongName = "skipstacks", HelpText = "Should skip stack migration.")]
        public bool SkipStacks = false;

        [Argument(ArgumentType.AtMostOnce, ShortName = "e", LongName = "skiperrors", HelpText = "Should skip error migration.")]
        public bool SkipErrors = false;

        [Argument(ArgumentType.AtMostOnce, ShortName = "d", LongName = "deleteindexes", HelpText = "Delete the existing indexes and start from scratch.")]
        public bool DeleteExistingIndexes = false;
    }
}