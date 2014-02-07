// Building a Better ExtractAssociatedIcon
// Bradley Smith - 2010/07/28

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CodeSmith.Core.Win32
{
    /// <summary>
    /// Defines a set of utility methods for extracting icons for files and file extensions.
    /// </summary>
    public static class FileIcon
    {
        #region Win32

        /// <summary>
        /// Retrieve the handle to the icon that represents the file and the index of the icon within the system image list. The handle is copied to the hIcon member of the structure specified by psfi, and the index is copied to the iIcon member.
        /// </summary>
        internal const uint SHGFI_ICON = 0x100;

        /// <summary>
        /// Modify SHGFI_ICON, causing the function to retrieve the file's large icon. The SHGFI_ICON flag must also be set.
        /// </summary>
        internal const uint SHGFI_LARGEICON = 0x0;

        /// <summary>
        /// Modify SHGFI_ICON, causing the function to retrieve the file's small icon. Also used to modify SHGFI_SYSICONINDEX, causing the function to return the handle to the system image list that contains small icon images. The SHGFI_ICON and/or SHGFI_SYSICONINDEX flag must also be set.
        /// </summary>
        internal const uint SHGFI_SMALLICON = 0x1;

        /// <summary>
        /// Retrieves information about an object in the file system, such as a file, folder, directory, or drive root.
        /// </summary>
        /// <param name="pszPath">A pointer to a null-terminated string of maximum length MAX_PATH that contains the path and file name. Both absolute and relative paths are valid.</param>
        /// <param name="dwFileAttributes">A combination of one or more file attribute flags (FILE_ATTRIBUTE_ values as defined in Winnt.h).</param>
        /// <param name="psfi">The address of a SHFILEINFO structure to receive the file information.</param>
        /// <param name="cbSizeFileInfo">The size, in bytes, of the SHFILEINFO structure pointed to by the psfi parameter.</param>
        /// <param name="uFlags">The flags that specify the file information to retrieve.</param>
        /// <returns>Nonzero if successful, or zero otherwise.</returns>
        [DllImport("shell32.dll")]
        private static extern IntPtr SHGetFileInfo(string pszPath,
                                                   uint dwFileAttributes,
                                                   ref SHFILEINFO psfi,
                                                   uint cbSizeFileInfo,
                                                   ShellIconSize uFlags);

        /// <summary>
        /// Creates an array of handles to large or small icons extracted from the specified executable file, DLL, or icon file. 
        /// </summary>
        /// <param name="libName">The name of an executable file, DLL, or icon file from which icons will be extracted.</param>
        /// <param name="iconIndex">The zero-based index of the first icon to extract. If this value is a negative number and either phiconLarge or phiconSmall is not NULL, the function begins by extracting the icon whose resource identifier is equal to the absolute value of nIconIndex. For example, use -3 to extract the icon whose resource identifier is 3.</param>
        /// <param name="largeIcon">An array of icon handles that receives handles to the large icons extracted from the file. If this parameter is NULL, no large icons are extracted from the file.</param>
        /// <param name="smallIcon">An array of icon handles that receives handles to the small icons extracted from the file. If this parameter is NULL, no small icons are extracted from the file.</param>
        /// <param name="nIcons">The number of icons to be extracted from the file.</param>
        /// <returns>If the nIconIndex parameter is -1, the phiconLarge parameter is NULL, and the phiconSmall  parameter is NULL, then the return value is the number of icons contained in the specified file. Otherwise, the return value is the number of icons successfully extracted from the file.</returns>
        [DllImport("Shell32.dll")]
        private static extern int ExtractIconEx(string libName,
                                               int iconIndex,
                                               IntPtr[] largeIcon,
                                               IntPtr[] smallIcon,
                                               uint nIcons);

        /// <summary>
        /// Contains information about a file object.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            /// <summary>
            /// A handle to the icon that represents the file.
            /// </summary>
            public IntPtr hIcon;

            /// <summary>
            /// The index of the icon image within the system image list.
            /// </summary>
            public IntPtr iIcon;

            /// <summary>
            /// An array of values that indicates the attributes of the file object.
            /// </summary>
            public uint dwAttributes;

            /// <summary>
            /// A string that contains the name of the file as it appears in the Windows Shell, or the path and file name of the file that contains the icon representing the file.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            /// <summary>
            /// A string that describes the type of file.
            /// </summary>
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        #endregion

        /// <summary>
        /// Returns an icon representation of the specified file.
        /// </summary>
        /// <param name="filename">The path to the file.</param>
        /// <param name="size">The desired size of the icon.</param>
        /// <returns>An icon that represents the file.</returns>
        public static Icon FromFile(string filename, ShellIconSize size)
        {
            var shinfo = new SHFILEINFO();
            SHGetFileInfo(filename, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), size);
            return Icon.FromHandle(shinfo.hIcon);
        }

        /// <summary>
        /// Returns the default icon representation for files with the specified extension.
        /// </summary>
        /// <param name="extension">File extension (including the leading period).</param>
        /// <param name="size">The desired size of the icon.</param>
        /// <returns>The default icon for files with the specified extension.</returns>
        public static Icon FromExtension(string extension, ShellIconSize size)
        {
            // locate the key corresponding to the file extension
            RegistryKey keyForExt = Registry.ClassesRoot.OpenSubKey(extension);
            if (keyForExt == null) return null;

            // the extension will point to a class name, leading to another key
            string className = Convert.ToString(keyForExt.GetValue(null));
            RegistryKey keyForClass = Registry.ClassesRoot.OpenSubKey(className);
            if (keyForClass == null) return null;

            // this key may have a DefaultIcon subkey
            RegistryKey keyForIcon = keyForClass.OpenSubKey("DefaultIcon");
            if (keyForIcon == null)
            {
                // if not, see if it has a CLSID subkey
                RegistryKey keyForCLSID = keyForClass.OpenSubKey("CLSID");
                if (keyForCLSID == null) return null;

                // the clsid value leads to another key that might contain DefaultIcon
                string clsid = "CLSID\\" + Convert.ToString(keyForCLSID.GetValue(null));
                keyForIcon = Registry.ClassesRoot.OpenSubKey(clsid + "\\DefaultIcon");
                if (keyForIcon == null) return null;
            }

            // the value of DefaultIcon will either be a path only or a path with a resource index
            string[] defaultIcon = Convert.ToString(keyForIcon.GetValue(null)).Split(',');
            int index = (defaultIcon.Length > 1) ? Int32.Parse(defaultIcon[1]) : 0;

            // get the requested icon
            var handles = new IntPtr[1];
            if (ExtractIconEx(defaultIcon[0],
                              index,
                              (size == ShellIconSize.LargeIcon) ? handles : null,
                              (size == ShellIconSize.SmallIcon) ? handles : null,
                              1) > 0)
                return Icon.FromHandle(handles[0]);
            
            return null;
        }
    }

    /// <summary>
    /// Represents the different icon sizes that can be extracted using the ExtractAssociatedIcon method.
    /// </summary>
    public enum ShellIconSize : uint
    {
        /// <summary>
        /// Specifies a small (16x16) icon.
        /// </summary>
        SmallIcon = FileIcon.SHGFI_ICON | FileIcon.SHGFI_SMALLICON,

        /// <summary>
        /// Specifies a large (32x32) icon.
        /// </summary>
        LargeIcon = FileIcon.SHGFI_ICON | FileIcon.SHGFI_LARGEICON
    }
}