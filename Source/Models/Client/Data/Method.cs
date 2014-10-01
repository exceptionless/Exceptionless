#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Models.Data {
    public class Method : IData {
        public Method() {
            Data = new DataDictionary();
            GenericArguments = new GenericArguments();
            Parameters = new ParameterCollection();
        }

        public bool IsSignatureTarget { get; set; }
        public string DeclaringNamespace { get; set; }
        public string DeclaringType { get; set; }

        public string Name { get; set; }

        public int ModuleId { get; set; }
        public DataDictionary Data { get; set; }
        public GenericArguments GenericArguments { get; set; }
        public ParameterCollection Parameters { get; set; }
    }
}