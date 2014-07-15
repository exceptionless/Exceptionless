#region Copyright 2014 Exceptionless

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// 
//     http://www.apache.org/licenses/LICENSE-2.0

#endregion

using System;
using System.Text;

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

        public string DeclaringTypeFullName {
            get {
                if (!String.IsNullOrEmpty(DeclaringNamespace) && !String.IsNullOrEmpty(DeclaringType))
                    return String.Concat(DeclaringNamespace, ".", DeclaringType.Replace('+', '.'));

                if (!String.IsNullOrEmpty(DeclaringType))
                    return DeclaringType.Replace('+', '.');

                return String.Empty;
            }
        }

        public string FullName {
            get {
                if (String.IsNullOrEmpty(Name))
                    return "<null>";

                var sb = new StringBuilder(DeclaringTypeFullName);
                sb.Append(Name);

                if (GenericArguments.Count <= 0)
                    return sb.ToString();

                sb.Append("[");
                bool first = true;
                foreach (string arg in GenericArguments) {
                    if (first)
                        first = false;
                    else
                        sb.Append(",");

                    sb.Append(arg);
                }

                sb.Append("]");

                return sb.ToString();
            }
        }

        public string Name { get; set; }

        public int ModuleId { get; set; }
        public DataDictionary Data { get; set; }
        public GenericArguments GenericArguments { get; set; }
        public ParameterCollection Parameters { get; set; }
    }
}