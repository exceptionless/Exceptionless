#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Exceptionless.Enrichments.Default;

namespace Exceptionless.Diagnostics {
    public class ExceptionlessTraceListener : TraceListener {
        public const int DEFAULT_MAX_ENTRIES_TO_STORE = 250;

        private readonly Queue<string> _innerList;
        private int _maxEntriesToStore;

        public ExceptionlessTraceListener() {
            _innerList = new Queue<string>();
            MaxEntriesToStore = DEFAULT_MAX_ENTRIES_TO_STORE;
        }

        public ExceptionlessTraceListener(int maxEntriesToStore) : this() {
            MaxEntriesToStore = maxEntriesToStore;
        }

        public ExceptionlessTraceListener(string maxEntriesToStore) : this() {
            int value;
            if (Int32.TryParse(maxEntriesToStore, out value))
                MaxEntriesToStore = value;
        }

        public int MaxEntriesToStore {
            get { return _maxEntriesToStore; }
            set {
                _maxEntriesToStore = value;
                
                if (_maxEntriesToStore <= 0)
                    InnerList.Clear();
            }
        }

        public override void WriteLine(string message) {
            Write(message);
        }

        public override void Write(string message) {
            if (MaxEntriesToStore <= 0 || String.IsNullOrEmpty(message))
                return;

            InnerList.Enqueue(message);

            while (InnerList.Count > Math.Max(0, MaxEntriesToStore))
                InnerList.Dequeue();
        }

        public override string ToString() {
            var output = new StringBuilder();

            foreach (string s in InnerList)
                output.Append(s);

            return output.ToString();
        }

        private Queue<string> InnerList { get { return _innerList; } }

        public List<string> GetLogEntries(int entryCount = TraceLogEnrichment.DefaultMaxEntriesToInclude) {
            return new List<string>(InnerList.Take(entryCount).ToArray());
        }
    }
}
