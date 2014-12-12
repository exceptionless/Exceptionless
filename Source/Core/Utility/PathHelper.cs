using System;
using System.IO;

namespace Exceptionless.Core.Utility {
    public static class PathHelper {
        private const string DATA_DIRECTORY = "|DataDirectory|";

        /// <summary>
        /// Expand the path, resolving the |DataDirectory| macro as appropriate.
        /// </summary>
        /// <param name="path">The path to expand</param>
        /// <returns>The expanded path</returns>
        public static string ExpandPath(string path) {
            if (String.IsNullOrEmpty(path))
                return path;

            if (!path.StartsWith(DATA_DIRECTORY, StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(path);

            string dataDirectory = GetDataDirectory();
            int length = DATA_DIRECTORY.Length;

            if (path.Length <= length)
                return dataDirectory;

            string relativePath = path.Substring(length);
            char c = relativePath[0];

            if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar)
                relativePath = relativePath.Substring(1);

            string fullPath = Path.Combine(dataDirectory, relativePath);
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