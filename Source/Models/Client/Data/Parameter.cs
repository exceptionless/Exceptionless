#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;

namespace Exceptionless.Models.Data {
    public class Parameter : IData {
        private readonly Lazy<DataDictionary> _data = new Lazy<DataDictionary>(() => new DataDictionary());

        public Parameter() {
            GenericArguments = new GenericArguments();
        }

        public string Name { get; set; }
        public string Type { get; set; }
        public string TypeNamespace { get; set; }

        public string TypeFullName {
            get {
                if (String.IsNullOrEmpty(Name))
                    return "<UnknownType>";

                return !String.IsNullOrEmpty(TypeNamespace) ? String.Concat(TypeNamespace, ".", Type.Replace('+', '.')) : Type.Replace('+', '.');
            }
        }

        public DataDictionary Data { get { return _data.Value; } }
        public GenericArguments GenericArguments { get; set; }
    }
}