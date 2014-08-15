using System;
using System.IO;

namespace Exceptionless.Extras.Utility {
    public static class PathHelper {
        private const string DATA_DIRECTORY = "|DataDirectory|";

        /// <summary>
        /// Expand the filename of the data source, resolving the |DataDirectory| macro as appropriate.
        /// </summary>
        /// <param name="sourceFile">The database filename to expand</param>
        /// <returns>The expanded path and filename of the filename</returns>
        public static string ExpandPath(string sourceFile) {
            if (String.IsNullOrEmpty(sourceFile))
                return sourceFile;

            if (!sourceFile.StartsWith(DATA_DIRECTORY, StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(sourceFile);

            string dataDirectory = GetDataDirectory();
            int length = DATA_DIRECTORY.Length;

            if (sourceFile.Length <= length)
                return dataDirectory;

            string path = sourceFile.Substring(length);
            char c = path[0];

            if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar)
                path = path.Substring(1);

            string fullPath = Path.Combine(dataDirectory, path);
            fullPath = Path.GetFullPath(fullPath);

            return fullPath;
        }

        /// <summary>
        /// Gets the data directory for the |DataDirectory| macro.
        /// </summary>
        /// <returns>The DataDirectory path.</returns>
        public static string GetDataDirectory() {
            string dataDirectory = AppDomain.CurrentDomain.GetData("DataDirectory") as string;
            if (String.IsNullOrEmpty(dataDirectory))
                dataDirectory = AppDomain.CurrentDomain.BaseDirectory;

            return Path.GetFullPath(dataDirectory);
        }
    }
}