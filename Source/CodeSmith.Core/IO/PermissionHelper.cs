#if !SILVERLIGHT

using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace CodeSmith.Core.IO
{
    public static class PermissionHelper
    {
        public static bool HasPermission(string filePath, FileSystemRights rights) {
            if (String.IsNullOrEmpty(filePath))
                throw new ArgumentNullException("filePath");

            if (!File.Exists(filePath) && !Directory.Exists(filePath))
                return false;

            try {
                FileSystemSecurity security = GetFileSystemSecurity(filePath);
                if (security == null)
                    return false;

                var currentuser = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                var rules = security.GetAccessRules(true, true, typeof(NTAccount));
                foreach (FileSystemAccessRule rule in rules) {
                    if ((rule.FileSystemRights & rights) == 0)
                        continue;

                    if (rule.IdentityReference.Value.StartsWith("S-1-")) {
                        var sid = new SecurityIdentifier(rule.IdentityReference.Value);
                        if (!currentuser.IsInRole(sid))
                            continue;
                    }

                    if (!currentuser.IsInRole(rule.IdentityReference.Value))
                        continue;

                    if (rule.AccessControlType == AccessControlType.Deny)
                        return false;

                    if (rule.AccessControlType == AccessControlType.Allow)
                        return true;
                }
            }
            catch (UnauthorizedAccessException) {
                return false;
            }

            return false;
        }

        public static bool SetPermission(string filePath, FileSystemRights rights) {
            if (String.IsNullOrEmpty(filePath))
                throw new ArgumentNullException("filePath");

            try {
                var rule = new FileSystemAccessRule(WindowsIdentity.GetCurrent().Name, rights, AccessControlType.Allow);
                FileSystemSecurity security = GetFileSystemSecurity(filePath);
                if (security == null)
                    return false;

                // Remove any access rules before adding them.
                var currentuser = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                var rules = security.GetAccessRules(true, true, typeof(NTAccount));
                foreach (FileSystemAccessRule r in rules) {
                    if ((r.FileSystemRights & rights) == 0)
                        continue;

                    if (r.IdentityReference.Value.StartsWith("S-1-")) {
                        var sid = new SecurityIdentifier(r.IdentityReference.Value);
                        if (!currentuser.IsInRole(sid))
                            continue;
                    }

                    if (r.AccessControlType == AccessControlType.Deny) {
                        security.RemoveAccessRuleAll(r);
                        break;
                    }
                }

                security.AddAccessRule(rule);

                if (File.Exists(filePath))
                    File.SetAccessControl(filePath, security as FileSecurity);
                else if (Directory.Exists(filePath))
                    Directory.SetAccessControl(filePath, security as DirectorySecurity);
                else
                    return false;

                return true;
            }
            catch (UnauthorizedAccessException) {
                return false;
            }
        }

        public static bool HasReadPermission(string filePath) {
            return HasPermission(filePath, FileSystemRights.Read | FileSystemRights.ReadData);
        }

        public static bool SetReadPermission(string filePath) {
            return SetPermission(filePath, FileSystemRights.Read | FileSystemRights.ReadData);
        }

        public static bool HasWritePermission(string filePath) {
            return HasPermission(filePath, FileSystemRights.Write);
        }

        public static bool SetWritePermission(string filePath) {
            var result = SetPermission(filePath, FileSystemRights.Write);
            if (!result)
                return false;

            try {
                var info = new FileInfo(filePath);
                if (info.Exists) {
                    if (info.IsReadOnly) {
                        info.IsReadOnly = false;

                        info.Refresh();
                    }

                    return !info.IsReadOnly;
                }
            } catch (Exception) {
                return false;
            }

            return true;
        }

        public static bool CanRead(string filePath) {
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check to see if the current user has read permissions to the file/folder.
            if (!HasReadPermission(filePath))
                return false;

            var directoryInfo = new DirectoryInfo(filePath);
            var info = new FileInfo(filePath);
            if (!directoryInfo.Exists && !info.Exists)
                return false;

            return true;
        }

        public static bool CanModify(string filePath) {
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check to see if the current user has write permissions to the file/folder.
            if (!HasWritePermission(filePath))
                return false;

            var info = new FileInfo(filePath);
            if (info.Exists)
                return !info.IsReadOnly;

            var directoryInfo = new DirectoryInfo(filePath);
            if (!directoryInfo.Exists)
                return false;

            return true;
        }

        public static FileSystemSecurity GetFileSystemSecurity(string filePath)
        {
            if (File.Exists(filePath))
                return File.GetAccessControl(filePath);

            if (Directory.Exists(filePath))
                return Directory.GetAccessControl(filePath);

            return null;
        }
    }
}
#endif