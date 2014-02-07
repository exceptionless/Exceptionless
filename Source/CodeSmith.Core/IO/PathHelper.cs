using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

#if !EMBEDDED
using CodeSmith.Core.Extensions;

namespace CodeSmith.Core.IO {
    public
#else
using Exceptionless.Extensions;

namespace Exceptionless.IO {
    internal
#endif

    static class PathHelper {
        private static readonly Regex _uniqueRegex = new Regex(@"\[(?<number>\d+)\]");

        private static readonly char[] _invalidPathChars;
        private static readonly char[] _invalidFileNameChars;

        private const string DATA_DIRECTORY = "|DataDirectory|";

        static PathHelper() {
            _invalidPathChars = Path.GetInvalidPathChars();
            Array.Sort(_invalidPathChars, 0, _invalidPathChars.Length);

#if SILVERLIGHT
            _invalidFileNameChars = new char[] { 
                '"', '<', '>', '|', '\0', '\x0001', '\x0002', '\x0003', '\x0004', '\x0005', '\x0006', '\a', '\b', '\t', '\n', '\v', 
                '\f', '\r', '\x000e', '\x000f', '\x0010', '\x0011', '\x0012', '\x0013', '\x0014', '\x0015', '\x0016', '\x0017', '\x0018', '\x0019', '\x001a', '\x001b', 
                '\x001c', '\x001d', '\x001e', '\x001f', ':', '*', '?', '\\', '/'
            };
#else
            _invalidFileNameChars = Path.GetInvalidFileNameChars();
#endif
            Array.Sort(_invalidFileNameChars, 0, _invalidFileNameChars.Length);
        }

        public static string Combine(params object[] paths) {
            if (paths == null)
                throw new ArgumentNullException("paths");
            if (paths.Length == 0)
                return String.Empty;

            string path = paths[0].ToString();

            for (int i = 1; i < paths.Length; i++) {
                if (paths[i] == null)
                    continue;

                string p = paths[i].ToString();
                if (!p.IsNullOrEmpty())
                    path = Path.Combine(path, p);
            }

            return File.Exists(path) ? Path.GetFullPath(path) : path;
        }

        /// <summary>
        ///	Creates a unique filename based on an existing filename
        /// </summary>
        /// <param name="fileSpec" type="string">A string containing the fully qualified path that will contain the new file</param>
        /// <returns>A string that contains the fully qualified path of the unique file name</returns>
        public static string GetUniqueName(string fileSpec) {
            if (String.IsNullOrEmpty(fileSpec))
                throw new ArgumentNullException("fileSpec");

            string folder = Path.GetDirectoryName(fileSpec);
            string name = Path.GetFileNameWithoutExtension(fileSpec);
            string extention = Path.GetExtension(fileSpec);

            while (File.Exists(fileSpec)) {
                Match numberMatch = _uniqueRegex.Match(name);
                if (numberMatch.Success) {
                    int number = int.Parse(numberMatch.Groups["number"].Value);
                    name = _uniqueRegex.Replace(name, String.Format("[{0}]", ++number));
                } else {
                    name += "[1]";
                }

                fileSpec = String.Concat(name, extention);
                fileSpec = Path.Combine(folder, fileSpec);
            }

            return fileSpec;
        }

        /// <summary>
        /// Removes illegal characters from a file path
        /// </summary>
        /// <param name="path">The file path</param>
        /// <returns>
        /// A string that contains the cleaned file path
        /// </returns>
        public static string GetCleanPath(string path) {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            var buffer = new StringBuilder();
            foreach (char t in path.Where(t => Array.BinarySearch(_invalidPathChars, t) < 0))
                buffer.Append(t);

            return buffer.ToString();
        }

        /// <summary>
        /// Removes illegal characters from a file name
        /// </summary>
        /// <param name="fileName">The file name</param>
        /// <param name="maxLength">The maximum length for the returned file name</param>
        /// <returns>
        /// A string that contains the cleaned file name
        /// </returns>
        public static string GetCleanFileName(string fileName, int maxLength = 20) {
            if (String.IsNullOrEmpty(fileName))
                throw new ArgumentNullException("fileName");

            var buffer = new StringBuilder();
            foreach (char t in fileName.Where(t => Array.BinarySearch(_invalidFileNameChars, t) < 0))
                buffer.Append(t);

            return buffer.ToString().Length > maxLength ? buffer.ToString().Substring(0, maxLength) : buffer.ToString();
        }

        /// <summary>
        /// Creates a relative path from one file or folder to another.
        /// </summary>
        /// <param name="fromDirectory">Contains the directory that defines the start of the relative path.</param>
        /// <param name="toPath">Contains the path that defines the endpoint of the relative path.</param>
        /// <returns>The relative path from the start directory to the end path.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static string RelativePathTo(string fromDirectory, string toPath) {
            if (fromDirectory == null)
                throw new ArgumentNullException("fromDirectory");

            if (toPath == null)
                throw new ArgumentNullException("toPath");

            bool isRooted = Path.IsPathRooted(fromDirectory) && Path.IsPathRooted(toPath);
            if (isRooted) {
                bool isDifferentRoot = !String.Equals(Path.GetPathRoot(fromDirectory), Path.GetPathRoot(toPath), StringComparison.OrdinalIgnoreCase);

                if (isDifferentRoot)
                    return toPath;
            }

            string[] fromDirectories = fromDirectory.Split(Path.DirectorySeparatorChar);
            string[] toDirectories = toPath.Split(Path.DirectorySeparatorChar);
            int length = Math.Min(fromDirectories.Length, toDirectories.Length);

            int lastCommonRoot = -1;

            // find common root
            for (int x = 0; x < length; x++) {
                if (!String.Equals(fromDirectories[x], toDirectories[x], StringComparison.OrdinalIgnoreCase))
                    break;

                lastCommonRoot = x;
            }

            if (lastCommonRoot == -1)
                return toPath;

            var relativePath = new List<string>();
            // add relative folders in from path
            for (int x = lastCommonRoot + 1; x < fromDirectories.Length; x++)
                if (fromDirectories[x].Length > 0)
                    relativePath.Add("..");

            // add to folders to path
            for (int x = lastCommonRoot + 1; x < toDirectories.Length; x++)
                relativePath.Add(toDirectories[x]);

            // create relative path
            var relativeParts = new string[relativePath.Count];
            relativePath.CopyTo(relativeParts, 0);

            string newPath = String.Join(Path.DirectorySeparatorChar.ToString(), relativeParts);

            return newPath;
        }

#if !SILVERLIGHT
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

        /// <summary>
        /// Creates the directory if it does not exist.
        /// </summary>
        /// <param name="path">The directory path to create.</param>
        public static void CreateDirectory(string path) {
            if (Path.HasExtension(path))
                path = Path.GetDirectoryName(path);

            if (Directory.Exists(path))
                return;

            Directory.CreateDirectory(path);
        }
#if !EMBEDDED
        /// <summary>
        /// Deletes the directory, subdirectories, and files in path if it exists.
        /// </summary>
        /// <param name="path">The directory path to delete.</param>
        public static void DeleteDirectory(string path) {
            if (!Directory.Exists(path))
                return;

            DeleteDirectoryInternal(path);
            Directory.Delete(path, true);
        }

        private static void DeleteDirectoryInternal(string path, string pattern = "*") {
            if (String.IsNullOrEmpty(pattern))
                throw new ArgumentException("Pattern cannot be null.", "pattern");

            //work around issues with Directory.Delete(path, true)
            var dir = new DirectoryInfo(path);

            foreach (var info in dir.GetFileSystemInfos(pattern, SearchOption.TopDirectoryOnly)) {
                // clear readonly and hidden flags
                if (info.Attributes.IsFlagOn(FileAttributes.ReadOnly))
                    info.Attributes = info.Attributes.SetFlagOff(FileAttributes.ReadOnly);
                if (info.Attributes.IsFlagOn(FileAttributes.Hidden))
                    info.Attributes = info.Attributes.SetFlagOff(FileAttributes.Hidden);

                if (info.Attributes.IsFlagOn(FileAttributes.Directory))
                    DeleteDirectoryInternal(info.FullName, pattern);

                info.Delete();
                info.Refresh();
            }
        }

        /// <summary>
        /// Deletes the all files in a specific directory that matches a pattern.
        /// </summary>
        /// <param name="directory">The directory path to delete.</param>
        /// <param name="pattern">The file pattern that should be used when deleting files.</param>
        public static void DeleteFiles(string directory, string pattern = "*") {
            if (!Directory.Exists(directory))
                return;

            DeleteFilesInternal(directory, pattern);
        }

        private static void DeleteFilesInternal(string path, string pattern = "*") {
            if (String.IsNullOrEmpty(pattern))
                throw new ArgumentException("Pattern cannot be null.", "pattern");

            //work around issues with Directory.Delete(path, true)
            var dir = new DirectoryInfo(path);

            foreach (var info in dir.GetFileSystemInfos(pattern, SearchOption.AllDirectories)) {
                if (info.Attributes.IsFlagOn(FileAttributes.Directory))
                    continue;

                // clear readonly and hidden flags
                if (info.Attributes.IsFlagOn(FileAttributes.ReadOnly))
                    info.Attributes = info.Attributes.SetFlagOff(FileAttributes.ReadOnly);
                if (info.Attributes.IsFlagOn(FileAttributes.Hidden))
                    info.Attributes = info.Attributes.SetFlagOff(FileAttributes.Hidden);

                info.Delete();
                info.Refresh();
            }
        }
#endif
#endif
    }
}