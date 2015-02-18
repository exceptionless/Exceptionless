/*
The MIT License (MIT)

Copyright (c) 2015 Stephen Reindl

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Exceptionless;
using Exceptionless.Dependency;
using Exceptionless.Extras.Utility;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using Exceptionless.Submission;

namespace SampleConsole
{
    public class FileSystemSubmissionClient : ISubmissionClient
    {
        /// <summary>
        /// Path to save intermediate responses
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public const string FileStoragePathKey = "FileSystemSubmissionPath";

        private string _storagePath;

        public SubmissionResponse PostEvents(IEnumerable<Event> events, ExceptionlessConfiguration config,
            IJsonSerializer serializer)
        {
            SetupFields(config);
            foreach (Event e in events)
            {
                byte[] data = Encoding.UTF8.GetBytes(serializer.Serialize(e));
                string referenceId = !string.IsNullOrWhiteSpace(e.ReferenceId)
                    ? e.ReferenceId
                    : Guid.NewGuid().ToString("D");
                e.Data[KnownDataKeys.RelayId] = referenceId;
                string path = Path.Combine(_storagePath, referenceId);
                Directory.CreateDirectory(path);
                path = Path.Combine(path, "event.elrep");
                File.WriteAllBytes(path, data);
            }
            return new SubmissionResponse(200);
        }

        private void SetupFields(ExceptionlessConfiguration config)
        {
            string apiKey = config.ApiKey;

            string value;
            if (config.Settings.TryGetValue(FileStoragePathKey, out value))
            {
                _storagePath = value;
            }
            else
            {
                _storagePath = config.ServerUrl;
            }
            _storagePath = PathHelper.ExpandPath(_storagePath);
            if (!DirectoryCanCreate(_storagePath, config))
            {
                config.Resolver.GetLog()
                    .Error(typeof (FileSystemSubmissionClient),
                        string.Format("Path defined in {0} ({1}) is not writable", FileStoragePathKey, _storagePath));
                return;
            }
        }

        public SubmissionResponse PostUserDescription(string referenceId, UserDescription description,
            ExceptionlessConfiguration config, IJsonSerializer serializer)
        {
            SetupFields(config);
            if (string.IsNullOrWhiteSpace(referenceId))
            {
                return new SubmissionResponse(404, "referenceId is not set");
            }
            string path = Path.Combine(_storagePath, referenceId, "user-data.elrep");
            byte[] data = Encoding.UTF8.GetBytes(serializer.Serialize(description));
            File.WriteAllBytes(path, data);
            return new SubmissionResponse(200);
        }

        public SettingsResponse GetSettings(ExceptionlessConfiguration config, IJsonSerializer serializer)
        {
            return new SettingsResponse(true);
        }

        /// <summary>
        /// Test a directory for create file access permissions
        /// </summary>
        /// <param name="directoryPath">Full directory path</param>
        /// <returns>State [bool]</returns>
        /// Credits: http://stackoverflow.com/a/21972598/2298807
        private static bool DirectoryCanCreate(string directoryPath, ExceptionlessConfiguration config)
        {
            if (string.IsNullOrEmpty(directoryPath)) return false;

            try
            {
                AuthorizationRuleCollection rules = Directory.GetAccessControl(directoryPath)
                    .GetAccessRules(true, true, typeof (SecurityIdentifier));
                WindowsIdentity identity = WindowsIdentity.GetCurrent();

                if (identity == null)
                {
                    return false; // working in a low trust environment, this might cause side effects.
                }
                foreach (FileSystemAccessRule rule in rules)
                {
                    if (identity.Groups != null)
                    {
                        if (identity.Groups.Contains(rule.IdentityReference))
                        {
                            if ((FileSystemRights.CreateFiles & rule.FileSystemRights) == FileSystemRights.CreateFiles)
                            {
                                if (rule.AccessControlType == AccessControlType.Allow)
                                    return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                config.Resolver.GetLog()
                    .Error(typeof(FileSystemSubmissionClient), ex, ex.Message);
            }
            return false;
        }

        public static class KnownDataKeys
        {
            public const string RelayId = "@relay_id";
        }
    }
}
