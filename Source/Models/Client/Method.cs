#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Text;

namespace Exceptionless.Models {
    public class Method {
        public Method() {
            ExtendedData = new DataDictionary();
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
                var sb = new StringBuilder();
                AppendMethod(sb, includeParameters: false);
                return sb.ToString();
            }
        }

        public string Signature {
            get {
                var sb = new StringBuilder();
                AppendMethod(sb);
                return sb.ToString();
            }
        }

        internal void AppendMethod(StringBuilder sb, bool includeParameters = true) {
            if (String.IsNullOrEmpty(Name)) {
                sb.Append("<null>");
                return;
            }

            if (!String.IsNullOrEmpty(DeclaringNamespace))
                sb.Append(DeclaringNamespace).Append(".");

            if (!String.IsNullOrEmpty(DeclaringType))
                sb.Append(DeclaringType.Replace('+', '.')).Append(".");

            sb.Append(Name);

            if (GenericArguments.Count > 0) {
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
            }

            if (includeParameters) {
                sb.Append("(");
                bool first = true;
                foreach (Parameter p in Parameters) {
                    if (first)
                        first = false;
                    else
                        sb.Append(", ");

                    if (String.IsNullOrEmpty(p.Type))
                        sb.Append("<UnknownType>");
                    else
                        sb.Append(p.Type.Replace('+', '.'));

                    sb.Append(" ");
                    sb.Append(p.Name);
                }
                sb.Append(")");
            }
        }

        public string Name { get; set; }

        public int ModuleId { get; set; }
        public DataDictionary ExtendedData { get; set; }
        public GenericArguments GenericArguments { get; set; }
        public ParameterCollection Parameters { get; set; }
    }
}