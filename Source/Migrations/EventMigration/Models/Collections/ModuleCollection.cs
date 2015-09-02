using System;
using System.Collections.ObjectModel;
using System.Linq;
using Exceptionless.Core.Extensions;

namespace Exceptionless.EventMigration.Models.Collections {
    public class ModuleCollection : Collection<Module> {
        public Exceptionless.Core.Models.ModuleCollection ToModules() {
            var modules = new Exceptionless.Core.Models.ModuleCollection();
            modules.AddRange(Items.Select(i => i.ToModule()));
            return modules;
        }
    }
}