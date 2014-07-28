#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using CodeSmith.Core.Extensions;

namespace Exceptionless.EventMigration.Models {
    public class Parameter {
        public Parameter() {
            ExtendedData = new ExtendedDataDictionary();
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

        public ExtendedDataDictionary ExtendedData { get; set; }
        public GenericArguments GenericArguments { get; set; }

        public Exceptionless.Models.Data.Parameter ToParameter() {
            var frame = new Exceptionless.Models.Data.Parameter {
                Name = Name,
                Type = Type,
                TypeNamespace = TypeNamespace
            };

            if (GenericArguments != null && GenericArguments.Count > 0)
                frame.GenericArguments = GenericArguments.ToGenericArguments();

            if (ExtendedData != null && ExtendedData.Count > 0)
                frame.Data.AddRange(ExtendedData.ToData());

            return frame;
        }
    }
}