#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Models.Data {
    public class Parameter : IData {
        public Parameter() {
            Data = new DataDictionary();
            GenericArguments = new GenericArguments();
        }

        public string Name { get; set; }
        public string Type { get; set; }
        public string TypeNamespace { get; set; }

        public DataDictionary Data { get; set; }
        public GenericArguments GenericArguments { get; set; }
    }
}