using System;
using Microsoft.Win32;
using System.IO;

namespace CodeSmith.Core.Helpers
{
    /// <summary>
    /// A class with file helper methods
    /// </summary>
    public static class FileHelper
    {
        private const string MimeKey = @"MIME\Database\Content Type";

        /// <summary>
        /// Gets the content type based on file extension .
        /// </summary>
        /// <param name="fileExtension">The file extension.</param>
        /// <returns>The content type.</returns>
        public static string GetContentType(string fileExtension)
        {
            if (String.IsNullOrEmpty(fileExtension))
                throw new ArgumentNullException("fileExtension");

            string contentType = GetCommon(fileExtension);
            if (String.IsNullOrEmpty(contentType))
                contentType = GetFromRegistry(fileExtension);

            return contentType;
        }

        private static string GetCommon(string fileExtension)
        {
            switch (fileExtension)
            {
                case ".png":
                    return "image/png";
                case ".gif":
                    return "image/gif";
                case ".jpeg":
                case ".jpg":
                    return "image/jpeg";
                case ".bmp":
                    return "image/bmp";
                case ".tif":
                case ".tiff":
                    return "image/tiff";

                case ".html":
                case ".htm":
                    return "text/html";
                case ".xml":
                    return "text/xml";
                case ".css":
                    return "text/css";
                case ".js":
                    return "text/plain";

                case ".zip":
                    return "application/zip";
                case ".gz":
                    return "application/x-gzip";

                case ".txt":
                    return "text/plain";

                case ".xsd":
                case ".xslt":
                    return "application/xml";

            }
            return String.Empty;
        }

        private static string GetFromRegistry(string fileExtension)
        {
            string contentType = String.Empty;

#if !SILVERLIGHT
            RegistryKey classesRoot = Registry.ClassesRoot;

            fileExtension = fileExtension.ToLower();

            //first, try extension directly
            using (RegistryKey typeKey = classesRoot.OpenSubKey(fileExtension, false))
            {
                if (typeKey != null)
                    contentType = typeKey.GetValue("Content Type", String.Empty) as string;
                if (!String.IsNullOrEmpty(contentType))
                    return contentType;
            }

            //if extension doesn't have contentType, search mime's
            using (RegistryKey typeKey = classesRoot.OpenSubKey(MimeKey, false))
            {
                foreach (string subKeyName in typeKey.GetSubKeyNames())
                {
                    string k = Path.Combine(MimeKey, subKeyName);
                    using (RegistryKey subKey = classesRoot.OpenSubKey(k, false))
                    {
                        string value = subKey.GetValue("Extension", String.Empty) as string;
                        if (fileExtension.Equals(value, StringComparison.OrdinalIgnoreCase))
                            return subKeyName;
                    }
                }
            }
#endif
            return contentType;
        }
    }
}
