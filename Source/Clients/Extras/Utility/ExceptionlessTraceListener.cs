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

namespace Exceptionless.Utility {
    public class ExceptionlessTraceListener : TraceListener {
        private readonly int _maxEntriesToStore;

        public ExceptionlessTraceListener(int maxEntriesToStore = 250) {
            _maxEntriesToStore = maxEntriesToStore;
            _innerList = new Queue<string>();
        }

        public override void WriteLine(string message) {
            Write(String.Concat(message, Environment.NewLine));
        }

        public override void Write(string message) {
            InnerList.Enqueue(message);

            while (InnerList.Count > _maxEntriesToStore)
                InnerList.Dequeue();
        }

        public override string ToString() {
            var output = new StringBuilder();

            foreach (string s in InnerList)
                output.Append(s);

            return output.ToString();
        }

        private readonly Queue<string> _innerList;
        private Queue<string> InnerList { get { return _innerList; } }

        public List<string> GetLogEntries(int entryCount = 10) {
            return new List<string>(InnerList.Take(entryCount).ToArray());
        }
    }
}
