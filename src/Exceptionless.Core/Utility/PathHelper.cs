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

            path = path.Replace('\\', Path.DirectorySeparatorChar);
            path = path.Replace('/', Path.DirectorySeparatorChar);

            if (!path.StartsWith(DATA_DIRECTORY, StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(path);

            string dataDirectory = GetDataDirectory();
            if (String.IsNullOrEmpty(dataDirectory))
                return path;

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
            string dataDirectory = Environment.GetEnvironmentVariable("WEBROOT_PATH");
            if (!String.IsNullOrEmpty(dataDirectory))
                dataDirectory = Path.Combine(dataDirectory, "App_Data");

            if (String.IsNullOrEmpty(dataDirectory))
                dataDirectory = AppDomain.CurrentDomain.GetData("DataDirectory") as string;

            if (String.IsNullOrEmpty(dataDirectory))
                dataDirectory = AppContext.BaseDirectory;

            return !String.IsNullOrEmpty(dataDirectory) ? Path.GetFullPath(dataDirectory) : null;
        }
    }
}