#region Copyright 2015 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using Exceptionless.Core.Extensions;

namespace Exceptionless.EventMigration.Models {
    public class Module {
        public Module() {
            ExtendedData = new ExtendedDataDictionary();
        }

        public int ModuleId { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public bool IsEntry { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public ExtendedDataDictionary ExtendedData { get; set; }

        public Exceptionless.Models.Data.Module ToModule() {
            var module = new Exceptionless.Models.Data.Module {
                ModuleId = ModuleId,
                Name = Name,
                Version = Version,
                IsEntry = IsEntry,
                CreatedDate = CreatedDate,
                ModifiedDate = ModifiedDate
            };

            if (ExtendedData != null && ExtendedData.Count > 0)
                module.Data.AddRange(ExtendedData.ToData());

            return module;
        }
    }
}