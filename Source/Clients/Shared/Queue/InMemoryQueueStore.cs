#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Logging;
using Exceptionless.Models;

namespace Exceptionless.Queue {
    internal class InMemoryQueueStore : IQueueStore {
        private readonly List<Tuple<Manifest, Error>> _data = new List<Tuple<Manifest, Error>>();

        public InMemoryQueueStore(int maxItems = 25) {
            MaxItems = maxItems;
            _data = new List<Tuple<Manifest, Error>>();
        }

        public int MaxItems { get; private set; }

        public bool VerifyStoreIsUsable() {
            return true;
        }

        public void Enqueue(Error error) {
            Manifest manifest = Manifest.FromError(error);
            manifest.LastAttempt = DateTime.MinValue;
            _data.Add(Tuple.Create(manifest, error));
            while (_data.Count > MaxItems)
                _data.RemoveAt(0);
        }

        public void UpdateManifest(Manifest manifest) {
            int index = _data.FindIndex(d => d.Item1.Id == manifest.Id);
            if (index >= 0)
                _data[index] = Tuple.Create(manifest, _data[index].Item2);
        }

        public void Delete(string id) {
            int index = _data.FindIndex(d => d.Item1.Id == id);
            if (index >= 0)
                _data.RemoveAt(index);
        }

        public int Cleanup(DateTime target) {
            int counter = 0;

            while (_data.Count > MaxItems) {
                _data.RemoveAt(0);
                counter++;
            }

            counter += _data.RemoveAll(m => m.Item1.LastAttempt.HasValue && m.Item1.LastAttempt != DateTime.MinValue && m.Item1.LastAttempt < target);

            return counter;
        }

        public Error GetError(string id) {
            int index = _data.FindIndex(d => d.Item1.Id == id);
            return index >= 0 ? _data[index].Item2 : null;
        }

        public IExceptionlessLogAccessor LogAccessor { get; set; }

        public virtual IEnumerable<Manifest> GetManifests(int? limit, bool includePostponed = true, DateTime? manifestsLastWriteTimeOlderThan = null) {
            IEnumerable<Tuple<Manifest, Error>> manifests;
            if (manifestsLastWriteTimeOlderThan.HasValue)
                manifests = _data.Where(m => includePostponed || m.Item1.ShouldRetry()).Where(m => m.Item1.LastAttempt.HasValue && m.Item1.LastAttempt < manifestsLastWriteTimeOlderThan.Value);
            else
                manifests = _data.Where(m => includePostponed || m.Item1.ShouldRetry());

            if (limit.HasValue)
                manifests = manifests.Take(limit.Value);

            return manifests.OrderBy(m => m.Item1.LastAttempt).Select(m => m.Item1);
        }
    }
}