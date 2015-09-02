using System;
using System.IO;
using System.Reflection;
using RazorEngine.Templating;
#pragma warning disable 618

namespace RazorSharpEmail {
    /// <summary>
    /// Resolves templates from a root directory.
    /// </summary>
    public class DirectoryTemplateResolver : ITemplateResolver {
        private readonly string _directoryFolder;

        /// <summary>
        /// Specify the root directory path to resolve templates from. Will look in a folder called "Templates" in the application directory if a rootDirectory is not specified.
        /// </summary>
        /// <param name="rootDirectory">The directory where the templates are located.</param>
        public DirectoryTemplateResolver(string rootDirectory = null) {
            // look in a folder called templates by default
            if (String.IsNullOrEmpty(rootDirectory))
                rootDirectory = "Templates";

            string resolvedRootDirectory = Path.IsPathRooted(rootDirectory) ? rootDirectory : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rootDirectory);
            if (!Path.IsPathRooted(rootDirectory)) {
                string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string binDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"bin", rootDirectory);

                if (!Directory.Exists(resolvedRootDirectory) && assemblyDirectory != null && Directory.Exists(Path.Combine(assemblyDirectory, rootDirectory)))
                    resolvedRootDirectory = Path.Combine(assemblyDirectory, rootDirectory);

                if (!Directory.Exists(resolvedRootDirectory) && Directory.Exists(binDirectory))
                    resolvedRootDirectory = binDirectory;
            }

            _directoryFolder = resolvedRootDirectory;
        }

        public string Resolve(string name) {
            if (name == null)
                throw new ArgumentNullException("name");

            return File.ReadAllText(Path.Combine(_directoryFolder, name));
        }
    }
}
