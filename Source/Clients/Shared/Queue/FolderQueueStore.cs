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
using Exceptionless.Serialization;

namespace Exceptionless.Queue {
    internal class FolderQueueStore : IQueueStore {
        private readonly string _queuePath;
        private static bool? _verified;

        public FolderQueueStore(string queuePath, IExceptionlessLogAccessor logAccessor) {
            LogAccessor = logAccessor;
            _queuePath = queuePath;

            try {
                if (!Directory.Exists(_queuePath))
                    Directory.CreateDirectory(_queuePath);
            } catch {}
        }

        public bool VerifyStoreIsUsable() {
            if (_verified.HasValue)
                return _verified.Value;

            _verified = false;
            if (String.IsNullOrEmpty(_queuePath))
                return false;

            try {
                if (!Directory.Exists(_queuePath))
                    return false;

                string path = Path.Combine(_queuePath, Path.GetRandomFileName());
                using (FileStream file = File.Create(path))
                    file.Close();

                File.Delete(path);
            } catch (Exception ex) {
                LogAccessor.Log.FormattedError(typeof(FolderQueueStore), "Problem trying to verify folder queue store: {0}", ex.Message);

                return false;
            }

            _verified = true;
            return true;
        }

        public void Enqueue(Error error) {
            if (error == null)
                throw new ArgumentNullException("error");

            try {
                string manifestPath = GetManifestPath(error.Id);
                string errorPath = GetErrorPath(error.Id);
                Manifest manifest = Manifest.FromError(error);
                ModelSerializer.Current.SerializeToFile(manifestPath, manifest);
                ModelSerializer.Current.SerializeToFile(errorPath, error);
            } catch (Exception ex) {
                LogAccessor.Log.FormattedError(typeof(FolderQueueStore), ex, "Problem enqueuing error '{0}' to folder '{1}'", error.Id, _queuePath);
                throw;
            }
        }

        public void UpdateManifest(Manifest manifest) {
            if (manifest == null)
                throw new ArgumentNullException("manifest");

            try {
                string manifestPath = GetManifestPath(manifest.Id);
                ModelSerializer.Current.SerializeToFile(manifestPath, manifest);
            } catch (Exception ex) {
                LogAccessor.Log.FormattedError(typeof(FolderQueueStore), ex, "Problem updating manifest '{0}' in folder '{1}'", manifest.Id, _queuePath);
                throw;
            }
        }

        public void Delete(string id) {
            // retry delete up to 3 times
            for (int retry = 0; retry < 3; retry++) {
                try {
                    string manifestPath = GetManifestPath(id);
                    if (File.Exists(manifestPath))
                        File.Delete(manifestPath);

                    string errorPath = GetErrorPath(id);
                    if (File.Exists(errorPath))
                        File.Delete(errorPath);

                    return;
                } catch (IOException ex) {
                    LogAccessor.Log.FormattedError(typeof(FolderQueueStore), ex, "Problem deleting id '{0}' from folder '{1}'", id, _queuePath);
                    Thread.Sleep(50);
                }
            }
        }

        public int Cleanup(DateTime target) {
            int counter = 0;

            // first delete all files older than the target date
            List<FileInfo> infos = GetFileInfos()
                .Where(m => m.CreationTime < target && !m.Extension.Equals(".config", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (FileInfo info in infos) {
                try {
                    info.Delete();
                    counter++;
                } catch (Exception ex) {
                    LogAccessor.Log.FormattedError(typeof(FolderQueueStore), ex, "Problem deleting queue file '{0}'", info.Name);
                }
            }

            // check to see if we have an excessive amount of manifests
            List<FileInfo> manifests = GetManifestsSortedByNewestCreateFirst().ToList();
            if (manifests.Count <= 250)
                return counter;

            // delete all but the newest 250
            foreach (FileInfo info in manifests.Skip(250)) {
                try {
                    var manifest = ModelSerializer.Current.Deserialize<Manifest>(info.OpenText());

                    if (manifest != null && manifest.CanDiscard) {
                        Delete(manifest.Id);
                        counter++;
                    }
                } catch (Exception ex) {
                    LogAccessor.Log.FormattedError(typeof(FolderQueueStore), ex, "Problem deleting queue file '{0}'", info.Name);
                }
            }

            return counter;
        }

        public Error GetError(string id) {
            if (String.IsNullOrEmpty(id))
                throw new ArgumentException("The id must be specified.", "id");

            Error error = null;
            string errorPath = GetErrorPath(id);
            if (!File.Exists(errorPath))
                return null;

            try {
                error = ModelSerializer.Current.Deserialize<Error>(errorPath);
            } catch (Exception ex) {
                LogAccessor.Log.FormattedError(typeof(FolderQueueStore), ex, "Problem deserializing error '{0}'", errorPath);
            }

            return error;
        }

        public IExceptionlessLogAccessor LogAccessor { get; set; }

        private IEnumerable<FileInfo> GetFileInfos() {
            return new DirectoryInfo(_queuePath).GetFiles().ToList();
        }

        private IEnumerable<FileInfo> GetManifestInfos() {
            const string searchPath = "*" + QueueManager.MANIFEST_EXTENSION;
            return new DirectoryInfo(_queuePath).GetFiles(searchPath, SearchOption.TopDirectoryOnly).ToList();
        }

        private IEnumerable<FileInfo> GetManifestsSortedByOldestWriteFirst(DateTime? manifestsLastWriteTimeOlderThan = null) {
            DateTime lastWriteTimeFilter = manifestsLastWriteTimeOlderThan ?? DateTime.Now;

            return GetManifestInfos().Where(m => m.LastWriteTime <= lastWriteTimeFilter).OrderBy(m => m.LastWriteTime);
        }

        private IEnumerable<FileInfo> GetManifestsSortedByNewestCreateFirst() {
            return GetManifestInfos().OrderByDescending(m => m.CreationTime);
        }

        public IEnumerable<Manifest> GetManifests(int? limit = null, bool includePostponed = true, DateTime? manifestsLastWriteTimeOlderThan = null) {
            var manifests = new List<Manifest>();
            IEnumerable<FileInfo> files;
            if (limit.HasValue)
                files = GetManifestsSortedByOldestWriteFirst(manifestsLastWriteTimeOlderThan).Take(limit.Value).ToList();
            else
                files = GetManifestsSortedByOldestWriteFirst(manifestsLastWriteTimeOlderThan);

            foreach (string file in files.Select(i => i.FullName)) {
                try {
                    var manifest = ModelSerializer.Current.Deserialize<Manifest>(file);

                    if (manifest != null && (includePostponed || manifest.ShouldRetry()))
                        manifests.Add(manifest);

                    if (limit.HasValue && manifests.Count == limit.Value)
                        break;
                } catch (Exception ex) {
                    LogAccessor.Log.FormattedError(typeof(FolderQueueStore), ex, "Problem reading manifest file '{0}'", file);

                    // if we can't deserialize the file, change the file extension so that we don't keep trying to parse it over and over.
                    string path = Path.ChangeExtension(file, "bad");
                    File.Move(file, path);
                    LogAccessor.Log.FormattedInfo(typeof(FolderQueueStore), "Moved bad manifest file '{0}' to '{1}'.", file, path);
                }
            }

            return manifests;
        }

        private string GetManifestPath(string id) {
            string path = Path.Combine(_queuePath, String.Concat(id, QueueManager.MANIFEST_EXTENSION));

            LogAccessor.Log.FormattedInfo(typeof(FolderQueueStore), "Getting manifest path '{0}'", path);
            return path;
        }

        private string GetErrorPath(string id) {
            string path = Path.Combine(_queuePath, String.Concat(id, QueueManager.ERROR_EXTENSION));

            LogAccessor.Log.FormattedInfo(typeof(FolderQueueStore), "Getting error path '{0}'", path);
            return path;
        }
    }
}