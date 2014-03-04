#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;

namespace Exceptionless.Models {
    public class Parameter {
        public Parameter() {
            ExtendedData = new DataDictionary();
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

        public DataDictionary ExtendedData { get; set; }
        public GenericArguments GenericArguments { get; set; }
    }
}