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

        public Exceptionless.Core.Models.Data.Module ToModule() {
            var module = new Exceptionless.Core.Models.Data.Module {
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