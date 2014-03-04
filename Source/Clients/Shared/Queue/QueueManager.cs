#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using Exceptionless.Extensions;
using Exceptionless.Logging;
using Exceptionless.Models;
using Exceptionless.Serialization;
using Exceptionless.Utility;

namespace Exceptionless.Queue {
    /// <summary>
    /// Used to manage the error queue files.
    /// </summary>
    internal class QueueManager {
        internal const string MANIFEST_EXTENSION = ".manifest";
        internal const string ERROR_EXTENSION = ".json";
        private readonly IConfigurationAndLogAccessor _accessors;

        public QueueManager(IConfigurationAndLogAccessor accessors) {
            _accessors = accessors;
        }

        public QueueManager(IConfigurationAndLogAccessor accessors, IQueueStore store) : this(accessors) {
            _store = store;
        }

        /// <summary>
        /// Occurs when the queue changes.
        /// </summary>
        public event EventHandler QueueChanged;

        private void OnQueueChanged(EventArgs e) {
            _accessors.Log.Info("Queue changed", typeof(QueueManager).Name);
            if (QueueChanged != null)
                QueueChanged(this, e);
        }

        private void OnQueueChanged() {
            OnQueueChanged(new EventArgs());
        }

        private IQueueStore _store;
        private IQueueStore Store { get { return _store ?? (_store = GetStore()); } }

        public void Enqueue(Error error) {
            if (String.IsNullOrEmpty(error.Id))
                error.Id = ObjectId.GenerateNewId().ToString();

            _accessors.Log.FormattedInfo(typeof(QueueManager), "Enqueuing error: {0}", error.Id);

            SerializeErrorExtendedData(_accessors, error);

            Store.Enqueue(error);
            OnQueueChanged();
        }

        internal static void SerializeErrorExtendedData(IConfigurationAndLogAccessor accessor, Error error) {
            if (error == null)
                return;

            if (error.Modules != null) {
                foreach (Module m in error.Modules)
                    SerializeDataObjectsToStrings(accessor, error.Id, m.ExtendedData);
            }

            if (error.RequestInfo != null)
                SerializeDataObjectsToStrings(accessor, error.Id, error.RequestInfo.ExtendedData);

            if (error.EnvironmentInfo != null)
                SerializeDataObjectsToStrings(accessor, error.Id, error.EnvironmentInfo.ExtendedData);

            ErrorInfo current = error;
            while (current != null) {
                SerializeDataObjectsToStrings(accessor, error.Id, current.ExtendedData);

                if (current.StackTrace != null) {
                    foreach (StackFrame s in current.StackTrace) {
                        SerializeDataObjectsToStrings(accessor, error.Id, s.ExtendedData);
                        if (s.Parameters != null) {
                            foreach (Parameter p in s.Parameters)
                                SerializeDataObjectsToStrings(accessor, error.Id, p.ExtendedData);
                        }
                    }
                }

                current = current.Inner;
            }
        }

        internal static void SerializeDataObjectsToStrings(IConfigurationAndLogAccessor accessor, string id, DataDictionary extendedData) {
            if (extendedData == null)
                return;

            // pre-serialize extended data objects so that we won't fail if we can't serialize them
            // also this allows us to log the exact one that caused an issue.
            var keys = new List<string>(extendedData.Keys);
            foreach (string key in keys) {
                if (key.AnyWildcardMatches(accessor.Configuration.DataExclusions, true))
                    continue;

                object data = extendedData[key];
                if (data is string)
                    continue;

                try {
                    extendedData[key] = ModelSerializer.Current.SerializeToString(data, excludedPropertyNames: accessor.Configuration.DataExclusions);
                } catch (Exception ex) {
                    accessor.Log.FormattedError(typeof(QueueManager), ex, "Unable to serialize extended data entry for '{0}' with key '{1}': {2}", id, key, ex.Message);
                    extendedData[key] = String.Format("Unable to serialize extended data key \"{0}\" of type \"{1}\". {2}", key, extendedData[key].GetType(), ex);
                }
            }
        }

        public void UpdateManifest(Manifest manifest) {
            _accessors.Log.FormattedInfo(typeof(QueueManager), "Updating manifest: {0}", manifest.Id);
            Store.UpdateManifest(manifest);
        }

        public void Delete(string id) {
            _accessors.Log.FormattedInfo(typeof(QueueManager), "Deleting error: {0}", id);

            Store.Delete(id);

            OnQueueChanged();
        }

        public Error GetError(string id) {
            _accessors.Log.FormattedInfo(typeof(QueueManager), "Getting error: {0}", id);

            return Store.GetError(id);
        }

        public int Cleanup(DateTime target) {
            _accessors.Log.FormattedInfo(typeof(QueueManager), "Cleaning up files older than: {0}", target);

            return Store.Cleanup(target);
        }

        public IEnumerable<Manifest> GetManifests(int? limit = null, bool includePostponed = true, DateTime? manifestsLastWriteTimeOlderThan = null) {
            _accessors.Log.Info("Getting Manifests", "QueueManager");

            return Store.GetManifests(limit, includePostponed, manifestsLastWriteTimeOlderThan);
        }

        private IQueueStore GetStore() {
            if (_accessors.Configuration.TestMode)
                return new InMemoryQueueStore();

#if !SILVERLIGHT
            if (!String.IsNullOrEmpty(_accessors.Configuration.QueuePath)) {
                try {
                    var store = new FolderQueueStore(_accessors.Configuration.QueuePath, _accessors);
                    if (store.VerifyStoreIsUsable())
                        return store;
                } catch (Exception ex) {
                    _accessors.Log.FormattedError(typeof(QueueManager), "Error trying to create folder store: {0}", ex.Message);
                }
            }
#endif

            try {
                var store = new IsolatedStorageQueueStore(_accessors.Configuration.StoreId, _accessors);
                if (store.VerifyStoreIsUsable())
                    return store;
            } catch (Exception ex) {
                _accessors.Log.FormattedError(typeof(QueueManager), "Error trying to create isolated storage store: {0}", ex.Message);
            }

            _accessors.Log.Info("Using in memory store provider.", "QueueManger");
            return new InMemoryQueueStore();
        }
    }
}