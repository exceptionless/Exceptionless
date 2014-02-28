#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Utility;

namespace Exceptionless.Queue {
    internal class IsolatedStorageQueueStore : IQueueStore {
        private static bool? _verified;

        public IsolatedStorageQueueStore(string subDirectory, IExceptionlessLogAccessor logAccessor) {
            LogAccessor = logAccessor;
            SubDirectory = subDirectory;
        }

        public string SubDirectory { get; private set; }

        public bool VerifyStoreIsUsable() {
            if (_verified.HasValue)
                return _verified.Value;

            _verified = false;

            try {
                if (String.IsNullOrEmpty(SubDirectory))
                    return false;

                string filename = Path.GetRandomFileName();

                using (IsolatedStorageDirectory dir = GetStorageDirectory()) {
                    dir.WriteFile(filename, "test");
                    dir.DeleteFile(filename);
                }
            } catch (Exception ex) {
                LogAccessor.Log.FormattedError(typeof(IsolatedStorageQueueStore), "Problem verifying isolated storage store: {0}", ex.Message);
                return false;
            }

            _verified = true;
            return true;
        }

        public void Enqueue(Error error) {
            if (error == null)
                throw new ArgumentNullException("error");

            try {
                using (IsolatedStorageDirectory dir = GetStorageDirectory()) {
                    string manifestFilename = GetManifestFilename(error.Id);
                    string errorFilename = GetErrorFilename(error.Id);
                    Manifest manifest = Manifest.FromError(error);
                    dir.WriteFile(manifestFilename, manifest);
                    dir.WriteFile(errorFilename, error);
                }
            } catch (Exception ex) {
                LogAccessor.Log.FormattedError(typeof(FolderQueueStore), ex, "Problem enqueuing error '{0}' to isolated storage", error.Id);
                throw;
            }
        }

        public void UpdateManifest(Manifest manifest) {
            if (manifest == null)
                throw new ArgumentNullException("manifest");

            try {
                string manifestFilename = GetManifestFilename(manifest.Id);
                using (IsolatedStorageDirectory dir = GetStorageDirectory())
                    dir.WriteFile(manifestFilename, manifest);
            } catch (Exception ex) {
                LogAccessor.Log.FormattedError(typeof(FolderQueueStore), ex, "Problem updating manifest '{0}' in isolated storage", manifest.Id);
                throw;
            }
        }

        private void Delete(IsolatedStorageDirectory dir, string id) {
            // retry delete up to 3 times
            for (int retry = 0; retry < 3; retry++) {
                try {
                    string manifestFilename = GetManifestFilename(id);
                    dir.DeleteFile(manifestFilename);

                    string errorFilename = GetErrorFilename(id);
                    dir.DeleteFile(errorFilename);

                    return;
                } catch (IOException ex) {
                    LogAccessor.Log.FormattedError(typeof(FolderQueueStore), ex, "Problem deleting id '{0}' from isolated storage", id);
                    Thread.Sleep(50);
                }
            }
        }

        public void Delete(string id) {
            // retry delete up to 3 times
            for (int retry = 0; retry < 3; retry++) {
                try {
                    using (IsolatedStorageDirectory dir = GetStorageDirectory()) {
                        string manifestFilename = GetManifestFilename(id);
                        dir.DeleteFile(manifestFilename);

                        string errorFilename = GetErrorFilename(id);
                        dir.DeleteFile(errorFilename);

                        return;
                    }
                } catch (IOException ex) {
                    LogAccessor.Log.FormattedError(typeof(FolderQueueStore), ex, "Problem deleting id '{0}' from isolated storage", id, id);
                    Thread.Sleep(50);
                }
            }
        }

        public int Cleanup(DateTime target) {
            int counter = 0;

            using (IsolatedStorageDirectory dir = GetStorageDirectory()) {
                // first delete all files older than the target date
                IEnumerable<IsolatedStorageFileInfo> files = dir.GetFilesWithTimes().Where(m => m.CreationTime < target && !m.FileName.EndsWith(".config", StringComparison.OrdinalIgnoreCase));

                foreach (IsolatedStorageFileInfo file in files) {
                    try {
                        if (dir.DeleteFile(file.FileName))
                            counter++;
                    } catch (Exception ex) {
                        LogAccessor.Log.FormattedError(typeof(IsolatedStorageQueueStore), ex, "Error deleting file '{0}' from isolated storage", file);
                    }
                }

                // check to see if we have an excessive amount of manifests
                List<IsolatedStorageFileInfo> manifests = GetManifestsSortedByNewestCreateFirst(dir);
                if (manifests.Count <= 250)
                    return counter;

                // delete all but the newest 250
                foreach (IsolatedStorageFileInfo file in manifests.Skip(250)) {
                    try {
                        var manifest = dir.ReadFile<Manifest>(file.FileName);
                        if (manifest == null || !manifest.CanDiscard)
                            continue;

                        Delete(dir, manifest.Id);
                        counter++;
                    } catch (Exception ex) {
                        LogAccessor.Log.FormattedError(typeof(IsolatedStorageQueueStore), ex, "Error deleting manifest file '{0}' from isolated storage", file);
                    }
                }
            }

            return counter;
        }

        public Error GetError(string id) {
            if (String.IsNullOrEmpty(id))
                throw new ArgumentException("The id must be specified.", "id");

            Error error = null;

            using (IsolatedStorageDirectory dir = GetStorageDirectory()) {
                string errorFilename = GetErrorFilename(id);
                try {
                    error = dir.ReadFile<Error>(errorFilename);
                } catch (Exception ex) {
                    LogAccessor.Log.FormattedError(typeof(FolderQueueStore), ex, "Problem deserializing error '{0}' from isolated storage", errorFilename);
                }
            }

            return error;
        }

        public IExceptionlessLogAccessor LogAccessor { get; set; }

        private List<IsolatedStorageFileInfo> GetManifestsSortedByOldestWriteFirst(IsolatedStorageDirectory dir, DateTime? manifestsLastWriteTimeOlderThan = null) {
            DateTime lastWriteTimeFilter = manifestsLastWriteTimeOlderThan.HasValue ? manifestsLastWriteTimeOlderThan.Value : DateTime.Now;
            return dir.GetFilesWithTimes("*" + QueueManager.MANIFEST_EXTENSION).Where(m => m.LastWriteTime.DateTime <= lastWriteTimeFilter).OrderBy(m => m.LastWriteTime).ToList();
        }

        private List<IsolatedStorageFileInfo> GetManifestsSortedByNewestCreateFirst(IsolatedStorageDirectory dir) {
            return dir.GetFilesWithTimes("*" + QueueManager.MANIFEST_EXTENSION).OrderByDescending(m => m.CreationTime).ToList();
        }

        public IEnumerable<Manifest> GetManifests(int? limit = null, bool includePostponed = true, DateTime? manifestsLastWriteTimeOlderThan = null) {
            var manifests = new List<Manifest>();

            using (IsolatedStorageDirectory dir = GetStorageDirectory()) {
                List<IsolatedStorageFileInfo> files = GetManifestsSortedByOldestWriteFirst(dir, manifestsLastWriteTimeOlderThan);

                foreach (IsolatedStorageFileInfo file in files) {
                    try {
                        var manifest = dir.ReadFile<Manifest>(file.FileName);

                        if (manifest != null && (includePostponed || manifest.ShouldRetry()))
                            manifests.Add(manifest);

                        if (limit.HasValue && manifests.Count == limit.Value)
                            break;
                    } catch (Exception ex) {
                        LogAccessor.Log.FormattedError(typeof(IsolatedStorageQueueStore), ex, "Error reading manifest file '{0}' from isolated storage", file);
                    }
                }
            }

            return manifests;
        }

        private string GetManifestFilename(string id) {
            return String.Concat(id, QueueManager.MANIFEST_EXTENSION);
        }

        private string GetErrorFilename(string id) {
            return String.Concat(id, QueueManager.ERROR_EXTENSION);
        }

        private IsolatedStorageDirectory GetStorageDirectory() {
            return new IsolatedStorageDirectory(SubDirectory);
        }
    }
}