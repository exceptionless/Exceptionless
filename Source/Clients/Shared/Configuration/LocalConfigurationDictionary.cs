#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.IsolatedStorage;
using System.Threading;
using Exceptionless.Extensions;
using Exceptionless.Json;
using Exceptionless.Logging;
using Exceptionless.Utility;

namespace Exceptionless.Configuration {
    public class LocalConfigurationDictionary : ObservableConcurrentDictionary<string, string> {
        public LocalConfigurationDictionary() : base(StringComparer.OrdinalIgnoreCase) {}

        protected override void NotifyObserversOfChange() {
            IsDirty = true;
            base.NotifyObserversOfChange();
        }

        public IExceptionlessLogAccessor LogAccessor { get; set; }

        [JsonIgnore]
        public bool IsDirty { get; internal set; }

        internal const string FileName = "Local.config";

        internal string StoreId { get; set; }

        #region Read/Write Properties

        [JsonIgnore]
        public TimeSpan QueuePoll { get { return new TimeSpan(QueuePollTicks); } set { QueuePollTicks = value.Ticks; } }

        [JsonIgnore]
        public long QueuePollTicks { get { return this.GetInt64("QueuePollTicks"); } set { this["QueuePollTicks"] = value.ToString(); } }

        [JsonIgnore]
        public Guid InstallIdentifier { get { return this.GetGuid("InstallIdentifier"); } set { this["InstallIdentifier"] = value.ToString(); } }

        [JsonIgnore]
        public DateTimeOffset InstallDate { get { return this.GetDateTimeOffset("InstallDate"); } set { this["InstallDate"] = value.ToString(); } }

        [JsonIgnore]
        public string EmailAddress { get { return this.GetString("EmailAddress"); } set { this["EmailAddress"] = value; } }

        [JsonIgnore]
        public int StartCount { get { return this.GetInt32("StartCount"); } set { this["StartCount"] = value.ToString(); } }

        [JsonIgnore]
        public int SubmitCount { get { return this.GetInt32("SubmitCount"); } set { this["SubmitCount"] = value.ToString(); } }

        [JsonIgnore]
        public int CurrentConfigurationVersion { get { return this.GetInt32("CurrentConfigurationVersion"); } set { this["CurrentConfigurationVersion"] = value.ToString(); } }

        [JsonIgnore]
        public DateTime NextConfigurationUpdate { get { return this.GetDateTime("NextConfigurationUpdate", DateTime.MinValue); } set { this["NextConfigurationUpdate"] = value.ToString(); } }

        #endregion

        public bool Save() {
            if (!IsDirty)
                return true;

            try {
                using (new SingleGlobalInstance(String.Concat(StoreId, FileName).GetHashCode().ToString(), 500)) {
                    if (!IsDirty)
                        return true;

                    LogAccessor.Log.Trace("Saving local configuration.", "LocalConfigurationDictionary");

                    // retry loop
                    for (int retry = 0; retry < 2; retry++) {
                        using (var dir = new IsolatedStorageDirectory(StoreId)) {
                            try {
                                dir.WriteFile(FileName, this);

                                // Only mark configuration as not dirty if everything was saved.
                                IsDirty = false;
                                LogAccessor.Log.Trace("Done saving local configuration.", "LocalConfigurationDictionary");
                                return true;
                            } catch (IsolatedStorageException ex) {
                                // File is being used by another process or thread or the file does not exist.
                                LogAccessor.Log.FormattedError(ex, "Unable to save data to local storage: {0}", ex.Message);
                                Thread.Sleep(50);
                            } catch (IOException ex) {
                                // File is being used by another process or thread or the file does not exist.
                                LogAccessor.Log.FormattedError(ex, "Unable to save data to local storage: {0}", ex.Message);
                                Thread.Sleep(50);
                            }
                        } // using
                    } // retry
                }
            } catch (Exception ex) {
                LogAccessor.Log.Error(ex, "An error occurred while saving local configuration");
            }

            return false;
        }

        private void LoadDefaults() {
            if (QueuePollTicks == 0) {
                QueuePollTicks = TimeSpan.FromSeconds(300).Ticks;
                IsDirty = true; // TODO: this can be removed once this property is updated in real time. Please see the the IsDirtyTests.
            }

            if (InstallIdentifier == Guid.Empty) {
                InstallIdentifier = Guid.NewGuid();
                IsDirty = true; // TODO: this can be removed once this property is updated in real time. Please see the the IsDirtyTests.
            }

            if (InstallDate == DateTimeOffset.MinValue) {
                InstallDate = DateTimeOffset.Now;
                IsDirty = true; // TODO: this can be removed once this property is updated in real time. Please see the the IsDirtyTests.
            }
        }

        internal static LocalConfigurationDictionary Create(string storeId, IExceptionlessLogAccessor logAccessor) {
            var localStorage = new LocalConfigurationDictionary {
                LogAccessor = logAccessor,
                StoreId = storeId,
                CurrentConfigurationVersion = 0,
                NextConfigurationUpdate = DateTime.MinValue
            };

            try {
                using (new SingleGlobalInstance(String.Concat(storeId, FileName).GetHashCode().ToString(), 500)) {
                    // retry loop
                    for (int retry = 0; retry < 2; retry++) {
                        using (var dir = new IsolatedStorageDirectory(storeId)) {
                            try {
                                if (!dir.FileExists(FileName))
                                    break;

                                var config = dir.ReadFile<LocalConfigurationDictionary>(FileName);
                                foreach (string key in config.Keys)
                                    localStorage[key] = config[key];

                                localStorage.IsDirty = false;
                                break;
                            } catch (Exception ex) {
                                // File is being used by another process or thread or the file does not exist.
                                logAccessor.Log.FormattedError(typeof(LocalConfigurationDictionary), ex, "Unable to read data from local storage: {0}", ex.Message);
                                Thread.Sleep(50);
                            }
                        } // using stream
                    } // retry 

                    // TODO: Why are we doing this even if the configuration couldn't be read or didn't exist??
                    localStorage.LoadDefaults();
                    localStorage.Save();

                    return localStorage;
                } // lock
            } catch (Exception ex) {
                logAccessor.Log.Error(ex, "An error occurred while saving local configuration");
                localStorage.LoadDefaults();
                return localStorage;
            }
        }
    }
}