using System;
using System.IO;
using Microsoft.Win32;

namespace CodeSmith.Core
{
    public class ContentType
    {
        private const string MimeKey = @"MIME\Database\Content Type";

        /// <summary>
        /// Gets the content type based on file extension .
        /// </summary>
        /// <param name="fileExtension">The file extension.</param>
        /// <returns>The content type.</returns>
        public static string GetByExtension(string fileExtension)
        {
            if (String.IsNullOrEmpty(fileExtension))
                throw new ArgumentNullException("fileExtension");

            string contentType = GetContentType(fileExtension);
            if (String.IsNullOrEmpty(contentType))
                contentType = GetContentTypeFromRegistry(fileExtension);

            return contentType;
        }

        public static string GetExtension(string contentType)
        {
            if (contentType == null) 
                throw new ArgumentNullException("contentType");

            string extension = GetByCommonExtension(contentType);
            if (String.IsNullOrEmpty(extension))
                extension = GetExtensionFromRegistry(contentType);

            return extension;
        }

        private static string GetExtensionFromRegistry(string contentType)
        {
#if !SILVERLIGHT
            using (RegistryKey subKey = Registry.ClassesRoot.OpenSubKey(Path.Combine(MimeKey, contentType), false))
                if (subKey != null)
                    return (string)subKey.GetValue("Extension", String.Empty);
#endif
            return String.Empty;
        }

        private static string GetByCommonExtension(string contentType)
        {
            switch (contentType)
            {
                case "image/png":
                    return ".png";
                case "image/gif":
                    return ".gif";
                case "image/jpeg":
                    return ".jpeg";
                case "image/bmp":
                    return ".bmp";
                case "image/tiff":
                    return ".tiff";

                case "text/html":
                    return ".html";
                case "text/xml":
                    return ".xml";
                case "text/css":
                    return ".css";
                case "text/plain":
                    return ".txt";

                case "application/zip":
                    return ".zip";
                case "application/x-gzip":
                    return ".gz";
            }
            return String.Empty;
        }

        private static string GetContentType(string fileExtension)
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

        private static string GetContentTypeFromRegistry(string fileExtension)
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

        /// <summary>
        /// Human-readable text and source code content types
        /// </summary>
        public static class Text
        {
            /// <summary>RichText; text/enriched</summary>
            public const string Enriched = "text/enriched";
            /// <summary>Html; text/html</summary>
            public const string Html = "text/html";
            /// <summary>Plain Text; text/plain</summary>
            public const string Plain = "text/plain";
            /// <summary>RichText; text/richtext</summary>
            public const string RichText = "text/richtext";
            /// <summary>Sgml; text/sgml</summary>
            public const string Sgml = "text/sgml";
            /// <summary>JavaScript; text/javascript</summary>
            public const string JavaScript = "text/javascript ";
            /// <summary>Cascading Style Sheets; text/css</summary>
            public const string Css = "text/css ";
            /// <summary>Xml; text/xml</summary>
            public const string Xml = "text/xml";
        }

        public static class Image
        {   
            public const string Gif = "image/gif";
            public const string Jpeg = "image/jpeg";
            public const string Png = "image/png";
            public const string Tiff = "image/tiff";            
        }

        public static class Multipart
        {
            public const string Mixed = "multipart/mixed";
            public const string Alternative = "multipart/alternative";
            public const string Related = "multipart/related";
            public const string FormData = "multipart/form-data";            
        }
    }
}
