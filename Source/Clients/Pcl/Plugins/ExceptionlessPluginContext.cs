#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Collections.Generic;

namespace Exceptionless.Plugins {
    public class ExceptionlessPluginContext {
        internal ExceptionlessPluginContext(ExceptionlessClient client, IEnumerable<KeyValuePair<string, object>> data = null) {
            Client = client;
            Data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (data != null) {
                foreach (var kvp in data)
                    Data.Add(kvp);
            }
        }

        public ExceptionlessClient Client { get; private set; }
        public IDictionary<string, object> Data { get; private set; }
    }
}